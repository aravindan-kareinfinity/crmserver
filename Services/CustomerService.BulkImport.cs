using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    public partial class CustomerService
    {
        /// <summary>Matches <c>core-crm-suite</c> <c>CustomerTypeId</c> fallbacks.</summary>
        private static class BulkCustomerTypeIds
        {
            public const int Lead = 88;
            public const int Prospect = 89;
            public const int Customer = 90;
        }

        private static readonly string[] BulkRequiredHeaders = { "reg_name" };

        public async Task<ApiResponse<BulkImportCustomersResultDto>> ImportCustomersFromSpreadsheetAsync(Stream stream, long? userId = null)
        {
            var auditUserId = userId.HasValue && userId.Value > 0 ? userId.Value : AuditUserIds.System;
            var fail = (string message, List<string>? rowErrors = null) =>
                new ApiResponse<BulkImportCustomersResultDto>
                {
                    Success = false,
                    Message = message,
                    Data = new BulkImportCustomersResultDto { RowErrors = rowErrors ?? new List<string>() }
                };

            List<List<string>> rows;
            try
            {
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheet(1);
                var used = ws.RangeUsed();
                if (used == null)
                    return fail("The first sheet is empty");

                var firstRow = used.FirstRow().RowNumber();
                var lastRow = used.LastRow().RowNumber();
                var firstCol = used.FirstColumn().ColumnNumber();
                var lastCol = used.LastColumn().ColumnNumber();

                rows = new List<List<string>>();
                for (var r = firstRow; r <= lastRow; r++)
                {
                    var line = new List<string>();
                    for (var c = firstCol; c <= lastCol; c++)
                        line.Add(CellText(ws.Cell(r, c)));
                    rows.Add(line);
                }
            }
            catch (Exception ex)
            {
                return fail($"Could not read Excel file: {ex.Message}");
            }

            if (rows.Count < 2)
                return fail("The first sheet must have a header row and at least one data row");

            var headerCells = rows[0];
            var col = MapHeaderRow(headerCells);
            foreach (var h in BulkRequiredHeaders)
            {
                if (!col.ContainsKey(h))
                    return fail($"Missing required column: {h}");
            }

            List<ReferenceEntry> entries = new();
            List<ExistingCustomerSnapshot> existingRows = new();
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var refsCmd = db.GetCommand(@"
SELECT id, category, label, value, is_active, sort_order, requires_implementation, is_implementation
FROM reference_entries
WHERE is_active = true;");
                using (DbDataReader reader = await db.Execute(refsCmd))
                {
                    while (await reader.ReadAsync())
                    {
                        entries.Add(new ReferenceEntry
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Category = reader.GetString(reader.GetOrdinal("category")),
                            Label = reader.GetString(reader.GetOrdinal("label")),
                            Value = reader.GetString(reader.GetOrdinal("value")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                            SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
                            RequiresImplementation = reader.IsDBNull(reader.GetOrdinal("requires_implementation"))
                                ? null
                                : reader.GetBoolean(reader.GetOrdinal("requires_implementation")),
                            IsImplementation = reader.IsDBNull(reader.GetOrdinal("is_implementation"))
                                ? null
                                : reader.GetBoolean(reader.GetOrdinal("is_implementation")),
                        });
                    }
                }

                var existingCmd = db.GetCommand(@"
SELECT id, reg_name, email, mobile
FROM customers;");
                using (DbDataReader reader2 = await db.Execute(existingCmd))
                {
                    while (await reader2.ReadAsync())
                    {
                        existingRows.Add(new ExistingCustomerSnapshot(
                            reader2.GetInt32(reader2.GetOrdinal("id")),
                            reader2.GetString(reader2.GetOrdinal("reg_name")),
                            reader2.IsDBNull(reader2.GetOrdinal("email")) ? null : reader2.GetString(reader2.GetOrdinal("email")),
                            reader2.IsDBNull(reader2.GetOrdinal("mobile")) ? null : reader2.GetString(reader2.GetOrdinal("mobile"))));
                    }
                }
            }

            var rowErrors = new List<string>();
            var staged = new List<(int RowIndex, string RegName, CreateCustomerDto Dto, string EmailNorm, string MobileNorm)>();

            for (var i = 1; i < rows.Count; i++)
            {
                var line = rows[i];
                if (line.All(string.IsNullOrWhiteSpace))
                    continue;

                var parsed = TryBuildBulkRow(entries, line, col, i + 1);
                if (parsed.Error != null)
                {
                    rowErrors.Add(parsed.Error);
                    continue;
                }

                var dto = parsed.Dto!;
                var emailNorm = NormalizeBulkEmail(dto.Email);
                var mobileNorm = NormalizeBulkMobile(dto.Mobile);
                staged.Add((parsed.RowIndex, parsed.RegName!, dto, emailNorm, mobileNorm));
            }

            AddIntraFileDuplicateErrors(staged, rowErrors);
            AddDatabaseDuplicateErrors(staged, existingRows, rowErrors);

            if (rowErrors.Count > 0)
            {
                SortRowErrors(rowErrors);
                return new ApiResponse<BulkImportCustomersResultDto>
                {
                    Success = false,
                    Message = $"{rowErrors.Count} issue(s); nothing was imported.",
                    Data = new BulkImportCustomersResultDto { RowErrors = rowErrors }
                };
            }

            if (staged.Count == 0)
                return fail("No data rows to import (empty sheet?)");

            try
            {
                var now = DateTime.UtcNow;
                var year = now.Year;
                var addedCustomers = new List<Customer>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    await db.BeginTransaction();
                    try
                    {
                        var reservedCodes = await ReserveCustomerCodesAsync(db, year, staged.Count);
                        for (var idx = 0; idx < staged.Count; idx++)
                        {
                            var s = staged[idx];
                            var customer = CustomerFromCreateDto(s.Dto, now, auditUserId);
                            customer.Code = reservedCodes[idx];

                            var insert = db.GetCommand(@"
INSERT INTO customers (
    code, reg_name, mobile, email,
    business_type_id, industry_id, lead_source_id,
    address_line1, address_line2,
    city_id, state_id, country_id,
    pincode, gst_number,
    contact_persons, emails, mobiles,
    shop_size_id, tier_id, type_id,
    is_active, total_locations, total_trade_names,
    created_at, created_by,
    converted_at, converted_by,
    prospect_converted_at, prospect_converted_by,
    customer_converted_at, customer_converted_by,
    pipeline_status, product_features_discussed,
    assigned_representative_id, interaction_mode_id,
    price_plan_selected, quotation_prepared_sent, quotation_accepted,
    advance_payment_received, invoice_generated, invoice_number,
    modified_at, modified_by
)
VALUES (
    @code, @reg_name, @mobile, @email,
    @business_type_id, @industry_id, @lead_source_id,
    @address_line1, @address_line2,
    @city_id, @state_id, @country_id,
    @pincode, @gst_number,
    @contact_persons, @emails, @mobiles,
    @shop_size_id, @tier_id, @type_id,
    true, NULL, NULL,
    @created_at, @created_by,
    NULL, NULL,
    NULL, NULL,
    NULL, NULL,
    NULL, false,
    NULL, NULL,
    false, false, false,
    false, false, NULL,
    @modified_at, @modified_by
)
RETURNING id;");
                            db.AddParameter(insert, "code", DbTypes.Types.String).Value = customer.Code;
                            db.AddParameter(insert, "reg_name", DbTypes.Types.String).Value = customer.RegName;
                            db.AddParameter(insert, "mobile", DbTypes.Types.String).Value = customer.Mobile ?? string.Empty;
                            db.AddParameter(insert, "email", DbTypes.Types.String).Value = customer.Email ?? string.Empty;
                            db.AddParameter(insert, "business_type_id", DbTypes.Types.Integer).Value = customer.BusinessTypeId.HasValue ? customer.BusinessTypeId.Value : DBNull.Value;
                            db.AddParameter(insert, "industry_id", DbTypes.Types.Integer).Value = customer.IndustryId.HasValue ? customer.IndustryId.Value : DBNull.Value;
                            db.AddParameter(insert, "lead_source_id", DbTypes.Types.Integer).Value = customer.LeadSourceId.HasValue ? customer.LeadSourceId.Value : DBNull.Value;
                            db.AddParameter(insert, "address_line1", DbTypes.Types.String).Value = customer.AddressLine1 ?? string.Empty;
                            db.AddParameter(insert, "address_line2", DbTypes.Types.String).Value = customer.AddressLine2 ?? (object)DBNull.Value;
                            db.AddParameter(insert, "city_id", DbTypes.Types.Integer).Value = customer.CityId.HasValue ? customer.CityId.Value : DBNull.Value;
                            db.AddParameter(insert, "state_id", DbTypes.Types.Integer).Value = customer.StateId.HasValue ? customer.StateId.Value : DBNull.Value;
                            db.AddParameter(insert, "country_id", DbTypes.Types.Integer).Value = customer.CountryId.HasValue ? customer.CountryId.Value : DBNull.Value;
                            db.AddParameter(insert, "pincode", DbTypes.Types.String).Value = customer.Pincode ?? string.Empty;
                            db.AddParameter(insert, "gst_number", DbTypes.Types.String).Value = customer.GstNumber ?? (object)DBNull.Value;
                            db.AddParameter(insert, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(customer.ContactPersons);
                            db.AddParameter(insert, "emails", DbTypes.Types.String).Value = JoinListToCsv(customer.Emails);
                            db.AddParameter(insert, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(customer.Mobiles);
                            db.AddParameter(insert, "shop_size_id", DbTypes.Types.Integer).Value = customer.ShopSizeId;
                            db.AddParameter(insert, "tier_id", DbTypes.Types.Integer).Value = customer.TierId;
                            db.AddParameter(insert, "type_id", DbTypes.Types.Integer).Value = customer.TypeId;
                            db.AddParameter(insert, "created_at", DbTypes.Types.DateTime).Value = now;
                            db.AddParameter(insert, "created_by", DbTypes.Types.Long).Value = auditUserId;
                            db.AddParameter(insert, "modified_at", DbTypes.Types.DateTime).Value = now;
                            db.AddParameter(insert, "modified_by", DbTypes.Types.Long).Value = auditUserId;

                            int newId = 0;
                            using (DbDataReader rr = await db.Execute(insert))
                            {
                                if (await rr.ReadAsync())
                                    newId = rr.GetInt32(rr.GetOrdinal("id"));
                            }
                            customer.Id = newId;

                            var tl = db.GetCommand(@"
INSERT INTO customer_timelines (
    customer_id, customer_code, type, notes, file_id, file_name,
    is_active, created_at, created_by, modified_at, modified_by
)
VALUES (
    @cid, @cc, 1, 'Imported via bulk upload', NULL, NULL,
    true, @created_at, @created_by, @modified_at, @modified_by
);");
                            db.AddParameter(tl, "cid", DbTypes.Types.Integer).Value = newId;
                            db.AddParameter(tl, "cc", DbTypes.Types.String).Value = customer.Code;
                            db.AddParameter(tl, "created_at", DbTypes.Types.DateTime).Value = now;
                            db.AddParameter(tl, "created_by", DbTypes.Types.Long).Value = auditUserId;
                            db.AddParameter(tl, "modified_at", DbTypes.Types.DateTime).Value = now;
                            db.AddParameter(tl, "modified_by", DbTypes.Types.Long).Value = auditUserId;
                            await db.ExecuteNonQuery(tl);

                            addedCustomers.Add(customer);
                        }

                        await db.CommitTransaction();
                    }
                    catch
                    {
                        await db.RollbackTransaction();
                        throw;
                    }
                }

                var bulkCreatorNames = await LoadCreatorDisplayNamesAsync(CustomerDisplayUserIdsMany(addedCustomers));

                return new ApiResponse<BulkImportCustomersResultDto>
                {
                    Success = true,
                    Message = $"Imported {addedCustomers.Count} contact(s)",
                    Data = new BulkImportCustomersResultDto
                    {
                        ImportedCount = addedCustomers.Count,
                        Created = addedCustomers
                            .Select(c => MapCustomer(c, bulkCreatorNames))
                            .ToList()
                    }
                };
            }
            catch (Exception ex)
            {
                return fail(ex.Message);
            }
        }

        private sealed record ExistingCustomerSnapshot(int Id, string RegName, string? Email, string? Mobile);

        private static Customer CustomerFromCreateDto(CreateCustomerDto dto, DateTime now, long auditUserId) => new()
        {
            RegName = dto.RegName,
            Mobile = dto.Mobile,
            Email = dto.Email,
            BusinessTypeId = dto.BusinessTypeId,
            IndustryId = dto.IndustryId,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            CityId = dto.CityId,
            StateId = dto.StateId,
            CountryId = dto.CountryId,
            Pincode = dto.Pincode,
            GstNumber = dto.GstNumber,
            ContactPersons = dto.ContactPersons,
            Emails = dto.Emails,
            Mobiles = dto.Mobiles,
            ShopSizeId = dto.ShopSizeId,
            TierId = dto.TierId,
            TypeId = dto.TypeId,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = auditUserId,
            ModifiedAt = now,
            ModifiedBy = auditUserId
        };

        private static string CellText(IXLCell cell)
        {
            if (cell.IsEmpty())
                return string.Empty;
            return cell.GetFormattedString().Trim();
        }

        private static string NormHeader(string h) =>
            Regex.Replace(
                Regex.Replace(h.Trim().ToLowerInvariant(), @"\s+", "_"),
                @"[^a-z0-9_]",
                "");

        private static Dictionary<string, int> MapHeaderRow(IReadOnlyList<string> cells)
        {
            var m = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < cells.Count; i++)
            {
                var k = NormHeader(cells[i] ?? "");
                if (k.Length > 0)
                    m[k] = i;
            }
            return m;
        }

        private static string GetCell(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> col, string key)
        {
            if (!col.TryGetValue(NormHeader(key), out var idx) || idx < 0 || idx >= row.Count)
                return "";
            return row[idx]?.Trim() ?? "";
        }

        private static int? FirstRefIdInCategory(List<ReferenceEntry> entries, string category) =>
            entries
                .Where(e => e.IsActive && string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Id)
                .Select(e => (int?)e.Id)
                .FirstOrDefault();

        private static int? ResolveRefId(List<ReferenceEntry> entries, string category, string raw)
        {
            var t = raw.Trim();
            if (t.Length == 0)
                return null;

            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
            {
                var byId = entries.FirstOrDefault(e =>
                    e.IsActive &&
                    string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase) &&
                    e.Id == n);
                if (byId != null)
                    return byId.Id;
            }

            var low = t.ToLowerInvariant();
            var list = entries.Where(e =>
                e.IsActive && string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
            return list.FirstOrDefault(e => e.Value.ToLowerInvariant() == low)?.Id
                ?? list.FirstOrDefault(e => e.Label.ToLowerInvariant() == low)?.Id;
        }

        private static List<string> SplitList(string raw)
        {
            var s = raw.Trim();
            if (s.Length == 0)
                return new List<string>();
            if (s.Contains('|'))
                return s.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            return s.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }

        private static string NormalizeBulkEmail(string email) => email.Trim().ToLowerInvariant();

        private static string NormalizeBulkMobile(string mobile)
        {
            var digits = Regex.Replace(mobile, @"\D", "");
            return digits.Length >= 10 ? digits[^10..] : digits;
        }

        private static (int RowIndex, string? RegName, CreateCustomerDto? Dto, string? Error) TryBuildBulkRow(
            List<ReferenceEntry> entries,
            IReadOnlyList<string> row,
            IReadOnlyDictionary<string, int> col,
            int rowIndex)
        {
            string Get(string k) => GetCell(row, col, k);

            var regName = Get("reg_name");
            var mobile = Get("mobile");
            var email = Get("email");
            var addressLine1 = Get("address_line_1");
            var pincode = Get("pincode");

            if (regName.Length == 0)
                return (rowIndex, "", null, $"Row {rowIndex}: reg_name is required");

            if (pincode.Trim().Length > 0 && !Regex.IsMatch(pincode.Trim(), @"^\d{6}$"))
                return (rowIndex, regName, null, $"Row {rowIndex}: pincode must be exactly 6 digits (got '{pincode}') — {regName}");

            var mobileDigits = NormalizeBulkMobile(mobile);
            if (mobile.Trim().Length > 0 && mobileDigits.Length != 10)
                return (rowIndex, regName, null, $"Row {rowIndex}: mobile must be 10 digits (got '{mobile}') — {regName}");

            var shopSizeRaw = Get("shop_size");
            var shopSizeId = ResolveRefId(entries, "Shop Size", shopSizeRaw);
            if (shopSizeId == null && shopSizeRaw.Trim().Length > 0)
                return (rowIndex, regName, null, $"Row {rowIndex}: Unknown shop_size: '{shopSizeRaw}' — {regName}");
            shopSizeId ??= FirstRefIdInCategory(entries, "Shop Size");
            if (shopSizeId == null)
                return (rowIndex, regName, null, $"Row {rowIndex}: No Shop Size reference data — {regName}");

            var tierRaw = Get("city_tier");
            var tierId = ResolveRefId(entries, "City Tier", tierRaw);
            if (tierId == null && tierRaw.Trim().Length > 0)
                return (rowIndex, regName, null, $"Row {rowIndex}: Unknown city_tier: '{tierRaw}' — {regName}");
            tierId ??= FirstRefIdInCategory(entries, "City Tier");
            if (tierId == null)
                return (rowIndex, regName, null, $"Row {rowIndex}: No City Tier reference data — {regName}");

            var typeRaw = Get("type");
            var typeId = ResolveRefId(entries, "Customer Type", typeRaw);
            if (typeId == null && typeRaw.Length > 0)
            {
                var v = typeRaw.ToLowerInvariant();
                if (v == "lead") typeId = BulkCustomerTypeIds.Lead;
                else if (v == "prospect") typeId = BulkCustomerTypeIds.Prospect;
                else if (v == "customer") typeId = BulkCustomerTypeIds.Customer;
            }

            if (typeId == null)
                typeId = BulkCustomerTypeIds.Lead;

            var contactPersons = SplitList(Get("contact_persons"));
            var mobilesList = SplitList(Get("mobiles"));
            var emailsList = SplitList(Get("emails"));

            var primaryMobiles = mobilesList.Count > 0
                ? mobilesList
                : mobile.Trim().Length > 0
                    ? new List<string> { mobile }
                    : new List<string>();
            var primaryEmails = emailsList.Count > 0
                ? emailsList
                : email.Trim().Length > 0
                    ? new List<string> { email }
                    : new List<string>();
            var persons = contactPersons.Count > 0 ? contactPersons : new List<string> { regName };

            var gst = Get("gst");
            var dto = new CreateCustomerDto
            {
                RegName = regName,
                Mobile = mobile,
                Email = email,
                BusinessTypeId = ResolveRefId(entries, "Business Type", Get("business_type")),
                IndustryId = ResolveRefId(entries, "Industry", Get("industry")),
                AddressLine1 = addressLine1,
                AddressLine2 = Get("address_line_2").Length > 0 ? Get("address_line_2") : null,
                CityId = ResolveRefId(entries, "City", Get("city")),
                StateId = ResolveRefId(entries, "State", Get("state")),
                CountryId = ResolveRefId(entries, "Country", Get("country")),
                Pincode = pincode,
                GstNumber = gst.Length > 0 ? gst : null,
                ContactPersons = persons,
                Mobiles = primaryMobiles,
                Emails = primaryEmails,
                ShopSizeId = shopSizeId.Value,
                TierId = tierId.Value,
                TypeId = typeId.Value
            };

            return (rowIndex, regName, dto, null);
        }

        private static void AddIntraFileDuplicateErrors(
            List<(int RowIndex, string RegName, CreateCustomerDto Dto, string EmailNorm, string MobileNorm)> staged,
            List<string> rowErrors)
        {
            var nameToRows = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var emailToRows = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var mobileToRows = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (var s in staged)
            {
                var nameNorm = NormalizeBulkRegName(s.RegName);
                if (nameNorm.Length > 0)
                {
                    if (!nameToRows.ContainsKey(nameNorm))
                        nameToRows[nameNorm] = new List<int>();
                    nameToRows[nameNorm].Add(s.RowIndex);
                }

                if (s.EmailNorm.Length > 0)
                {
                    if (!emailToRows.ContainsKey(s.EmailNorm))
                        emailToRows[s.EmailNorm] = new List<int>();
                    emailToRows[s.EmailNorm].Add(s.RowIndex);
                }

                if (s.MobileNorm.Length > 0)
                {
                    if (!mobileToRows.ContainsKey(s.MobileNorm))
                        mobileToRows[s.MobileNorm] = new List<int>();
                    mobileToRows[s.MobileNorm].Add(s.RowIndex);
                }
            }

            foreach (var g in nameToRows.Values.Where(x => x.Count >= 2))
            {
                var sorted = g.Distinct().OrderBy(x => x).ToList();
                foreach (var ri in sorted)
                {
                    var others = sorted.Where(x => x != ri).ToList();
                    rowErrors.Add($"Row {ri}: duplicate registered name in this file (same as row(s) {string.Join(", ", others)})");
                }
            }

            foreach (var g in emailToRows.Values.Where(x => x.Count >= 2))
            {
                var sorted = g.Distinct().OrderBy(x => x).ToList();
                foreach (var ri in sorted)
                {
                    var others = sorted.Where(x => x != ri).ToList();
                    rowErrors.Add($"Row {ri}: duplicate email in this file (same as row(s) {string.Join(", ", others)})");
                }
            }

            foreach (var g in mobileToRows.Values.Where(x => x.Count >= 2))
            {
                var sorted = g.Distinct().OrderBy(x => x).ToList();
                foreach (var ri in sorted)
                {
                    var others = sorted.Where(x => x != ri).ToList();
                    rowErrors.Add($"Row {ri}: duplicate mobile in this file (same as row(s) {string.Join(", ", others)})");
                }
            }
        }

        private static void AddDatabaseDuplicateErrors(
            List<(int RowIndex, string RegName, CreateCustomerDto Dto, string EmailNorm, string MobileNorm)> staged,
            List<ExistingCustomerSnapshot> existingRows,
            List<string> rowErrors)
        {
            foreach (var s in staged)
            {
                var regNorm = NormalizeBulkRegName(s.RegName);
                if (regNorm.Length > 0)
                {
                    foreach (var ex in existingRows)
                    {
                        if (NormalizeBulkRegName(ex.RegName) == regNorm)
                        {
                            rowErrors.Add($"Row {s.RowIndex}: duplicate registered name already in CRM — \"{ex.RegName}\" (id {ex.Id})");
                            break;
                        }
                    }
                }

                if (s.EmailNorm.Length > 0)
                {
                    foreach (var ex in existingRows)
                    {
                        if (NormalizeBulkEmail(ex.Email ?? "") == s.EmailNorm)
                        {
                            rowErrors.Add($"Row {s.RowIndex}: duplicate email already in CRM — \"{ex.RegName}\" (id {ex.Id})");
                            break;
                        }
                    }
                }

                if (s.MobileNorm.Length > 0)
                {
                    foreach (var ex in existingRows)
                    {
                        if (NormalizeBulkMobile(ex.Mobile ?? "") == s.MobileNorm)
                        {
                            rowErrors.Add($"Row {s.RowIndex}: duplicate mobile already in CRM — \"{ex.RegName}\" (id {ex.Id})");
                            break;
                        }
                    }
                }
            }
        }

        private static string NormalizeBulkRegName(string regName)
        {
            var s = (regName ?? "").Trim().ToLowerInvariant();
            if (s.Length == 0) return "";
            // Normalize internal whitespace so "Rockfort  Collection" == "Rockfort Collection"
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        private static void SortRowErrors(List<string> rowErrors)
        {
            rowErrors.Sort((a, b) =>
            {
                var ma = Regex.Match(a, @"^Row (\d+)");
                var mb = Regex.Match(b, @"^Row (\d+)");
                if (ma.Success && mb.Success &&
                    int.TryParse(ma.Groups[1].Value, out var ra) &&
                    int.TryParse(mb.Groups[1].Value, out var rb))
                    return ra.CompareTo(rb);
                return string.Compare(a, b, StringComparison.Ordinal);
            });
        }
    }
}
