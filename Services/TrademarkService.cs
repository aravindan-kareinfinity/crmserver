using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    public interface ITrademarkService
    {
        Task<ApiResponse<List<TrademarkResponseDto>>> GetAll();
        Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomerCode(string customerCode, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<TrademarkResponseDto>> GetTrademarkById(int id);
        Task<ApiResponse<List<TrademarkResponseDto>>> GetTrademarksByActive(bool isActive);
        Task<ApiResponse<TrademarkResponseDto>> CreateTrademark(CreateTrademarkDto dto);
        Task<ApiResponse<TrademarkResponseDto>> UpdateTrademark(int id, UpdateTrademarkDto dto);
        Task<ApiResponse<bool>> DeleteTrademark(int id);
    }

    public class TrademarkService : ITrademarkService
    {
        IDbProvider dbprovider;

        public TrademarkService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private static TrademarkResponseDto Map(Trademark t) => new()
        {
            Id = t.Id,
            CustomerId = t.Customer?.Id ?? t.CustomerId,
            CustomerCode = t.CustomerCode,
            LocationId = t.LocationId,
            LocationCode = t.Location?.Code,
            RegName = t.RegName,
            GstNumber = t.GstNumber,
            Pincode = t.Pincode,
            CityId = t.CityId,
            StateId = t.StateId,
            CountryId = t.CountryId,
            AddressLine1 = t.AddressLine1,
            AddressLine2 = t.AddressLine2,
            ContactPersons = t.ContactPersons,
            Emails = t.Emails,
            Mobiles = t.Mobiles,
            TierId = t.TierId,
            ShopSizeId = t.ShopSizeId,
            RegistrationNumber = t.RegistrationNumber,
            Category = t.Category,
            Description = t.Description,
            RegistrationDate = t.RegistrationDate,
            ExpiryDate = t.ExpiryDate,
            IsEnabled = t.IsEnabled,
            IsActive = t.IsActive,
            Remarks = t.Remarks,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            ModifiedAt = t.ModifiedAt,
            ModifiedBy = t.ModifiedBy
        };

        private static List<string> SplitCsvToList(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        private static string JoinListToCsv(List<string>? list) =>
            list == null ? string.Empty : string.Join(",", list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));

        private static Trademark ReadTrademark(DbDataReader r)
        {
            return new Trademark
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                CustomerId = r.GetInt32(r.GetOrdinal("customer_id")),
                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                LocationId = r.GetInt32(r.GetOrdinal("location_id")),
                RegName = r.GetString(r.GetOrdinal("reg_name")),
                GstNumber = r.GetString(r.GetOrdinal("gst_number")),
                Pincode = r.GetString(r.GetOrdinal("pincode")),
                CityId = r.GetInt32(r.GetOrdinal("city_id")),
                StateId = r.GetInt32(r.GetOrdinal("state_id")),
                CountryId = r.IsDBNull(r.GetOrdinal("country_id")) ? null : r.GetInt32(r.GetOrdinal("country_id")),
                AddressLine1 = r.GetString(r.GetOrdinal("address_line1")),
                AddressLine2 = r.IsDBNull(r.GetOrdinal("address_line2")) ? null : r.GetString(r.GetOrdinal("address_line2")),
                ContactPersons = SplitCsvToList(r.GetString(r.GetOrdinal("contact_persons"))),
                Emails = SplitCsvToList(r.GetString(r.GetOrdinal("emails"))),
                Mobiles = SplitCsvToList(r.GetString(r.GetOrdinal("mobiles"))),
                TierId = r.GetInt32(r.GetOrdinal("tier_id")),
                ShopSizeId = r.IsDBNull(r.GetOrdinal("shop_size_id")) ? null : r.GetInt32(r.GetOrdinal("shop_size_id")),
                RegistrationNumber = r.IsDBNull(r.GetOrdinal("registration_number")) ? null : r.GetString(r.GetOrdinal("registration_number")),
                Category = r.IsDBNull(r.GetOrdinal("category")) ? null : r.GetString(r.GetOrdinal("category")),
                Description = r.IsDBNull(r.GetOrdinal("description")) ? null : r.GetString(r.GetOrdinal("description")),
                RegistrationDate = r.IsDBNull(r.GetOrdinal("registration_date")) ? null : r.GetDateTime(r.GetOrdinal("registration_date")),
                ExpiryDate = r.IsDBNull(r.GetOrdinal("expiry_date")) ? null : r.GetDateTime(r.GetOrdinal("expiry_date")),
                IsEnabled = r.GetBoolean(r.GetOrdinal("is_enabled")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                Remarks = r.IsDBNull(r.GetOrdinal("remarks")) ? null : r.GetString(r.GetOrdinal("remarks")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                CreatedBy = r.IsDBNull(r.GetOrdinal("created_by")) ? null : r.GetInt64(r.GetOrdinal("created_by")),
                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
            };
        }

        private async Task<string?> GetCustomerCodeByIdAsync(IDb db, int customerId)
        {
            if (customerId <= 0) return null;
            var cmd = db.GetCommand("SELECT code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync())
                    return r.IsDBNull(r.GetOrdinal("code")) ? null : r.GetString(r.GetOrdinal("code"));
            }
            return null;
        }

        private async Task<int?> GetCustomerIdByCodeAsync(IDb db, string customerCode)
        {
            var cmd = db.GetCommand("SELECT id FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
            db.AddParameter(cmd, "code", DbTypes.Types.String).Value = customerCode.Trim();
            using (DbDataReader r = await db.Execute(cmd))
            {
                if (await r.ReadAsync())
                    return r.GetInt32(r.GetOrdinal("id"));
            }
            return null;
        }

        private async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(IDb db, int customerId, string? customerCode)
        {
            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                var trimmed = customerCode.Trim();
                var cmd = db.GetCommand("SELECT id, code FROM customers WHERE code=@code AND is_active=true LIMIT 1;");
                db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                using (DbDataReader r = await db.Execute(cmd))
                {
                    if (!await r.ReadAsync())
                        return (string.Empty, 0, $"Unknown customer code: \"{trimmed}\"");
                    return (r.GetString(r.GetOrdinal("code")), r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            if (customerId <= 0)
                return (string.Empty, 0, "Provide customerId or customerCode");

            var byId = db.GetCommand("SELECT id, code FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
            db.AddParameter(byId, "id", DbTypes.Types.Integer).Value = customerId;
            using (DbDataReader r2 = await db.Execute(byId))
            {
                if (!await r2.ReadAsync())
                    return (string.Empty, 0, "Customer not found or has no code assigned");
                var code = r2.IsDBNull(r2.GetOrdinal("code")) ? "" : r2.GetString(r2.GetOrdinal("code"));
                if (string.IsNullOrWhiteSpace(code))
                    return (string.Empty, 0, "Customer not found or has no code assigned");
                return (code, r2.GetInt32(r2.GetOrdinal("id")), null);
            }
        }

        private async Task<(int LocationId, string? Error)> ResolveRequiredLocationIdAsync(
            IDb db,
            string customerCode,
            int locationId,
            string? locationCode)
        {
            if (string.IsNullOrWhiteSpace(customerCode))
                return (0, "Customer code is required to resolve location");

            var cc = customerCode.Trim();
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                var trimmed = locationCode.Trim();
                var cmd = db.GetCommand(@"
SELECT id
FROM locations
WHERE customer_code=@cc AND code=@code
LIMIT 1;");
                db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                db.AddParameter(cmd, "code", DbTypes.Types.String).Value = trimmed;
                using (DbDataReader r = await db.Execute(cmd))
                {
                    if (!await r.ReadAsync())
                        return (0, $"Unknown location code \"{trimmed}\" for this customer");
                    return (r.GetInt32(r.GetOrdinal("id")), null);
                }
            }

            if (locationId <= 0)
                return (0, "Provide locationId or locationCode");
            return (locationId, null);
        }

        public async Task<ApiResponse<List<TrademarkResponseDto>>> GetAll()
        {
            try
            {
                var dtos = new List<TrademarkResponseDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT
  t.*,
  c.id AS customer_id,
  l.code AS location_code
FROM trademarks t
JOIN customers c ON c.code = t.customer_code
LEFT JOIN locations l ON l.id = t.location_id
ORDER BY t.id DESC;");
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            var t = ReadTrademark(r);
                            t.Customer = new Customer { Id = r.GetInt32(r.GetOrdinal("customer_id")) };
                            t.Location = new Location { Id = t.LocationId, Code = r.IsDBNull(r.GetOrdinal("location_code")) ? "" : r.GetString(r.GetOrdinal("location_code")) };
                            dtos.Add(Map(t));
                        }
                    }
                }
                return new ApiResponse<List<TrademarkResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomer(int customerId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                string? cc;
                using (IDb db0 = await dbprovider.GetDb())
                {
                    await db0.Connect();
                    cc = await GetCustomerCodeByIdAsync(db0, customerId);
                }
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<PaginatedResponse<TrademarkResponseDto>>
                    {
                        Success = true,
                        Data = new PaginatedResponse<TrademarkResponseDto>
                        {
                            Items = new List<TrademarkResponseDto>(),
                            Total = 0,
                            PageNumber = pageNumber,
                            PageSize = pageSize
                        }
                    };
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                int total = 0;
                var dtos = new List<TrademarkResponseDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand("SELECT COUNT(*)::int AS total FROM trademarks WHERE customer_code=@cc;");
                    db.AddParameter(countCmd, "cc", DbTypes.Types.String).Value = cc;
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync())
                            total = r.GetInt32(r.GetOrdinal("total"));
                    }

                    var cmd = db.GetCommand(@"
SELECT
  t.*,
  c.id AS customer_id,
  l.code AS location_code
FROM trademarks t
JOIN customers c ON c.code = t.customer_code
LEFT JOIN locations l ON l.id = t.location_id
WHERE t.customer_code=@cc
ORDER BY t.id DESC
LIMIT @limit OFFSET @offset;");
                    db.AddParameter(cmd, "cc", DbTypes.Types.String).Value = cc;
                    db.AddParameter(cmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(cmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(cmd))
                    {
                        while (await r2.ReadAsync())
                        {
                            var t = ReadTrademark(r2);
                            t.Customer = new Customer { Id = r2.GetInt32(r2.GetOrdinal("customer_id")) };
                            t.Location = new Location { Id = t.LocationId, Code = r2.IsDBNull(r2.GetOrdinal("location_code")) ? "" : r2.GetString(r2.GetOrdinal("location_code")) };
                            dtos.Add(Map(t));
                        }
                    }
                }
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<TrademarkResponseDto>
                    {
                        Items = dtos,
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> GetTrademarkById(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT
  t.*,
  c.id AS customer_id,
  l.code AS location_code
FROM trademarks t
JOIN customers c ON c.code = t.customer_code
LEFT JOIN locations l ON l.id = t.location_id
WHERE t.id=@id
LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (!await r.ReadAsync())
                            return new ApiResponse<TrademarkResponseDto> { Success = false, Message = "Trademark not found" };
                        var t = ReadTrademark(r);
                        t.Customer = new Customer { Id = r.GetInt32(r.GetOrdinal("customer_id")) };
                        t.Location = new Location { Id = t.LocationId, Code = r.IsDBNull(r.GetOrdinal("location_code")) ? "" : r.GetString(r.GetOrdinal("location_code")) };
                        return new ApiResponse<TrademarkResponseDto> { Success = true, Data = Map(t) };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<TrademarkResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<TrademarkResponseDto>>> GetTrademarksByActive(bool isActive)
        {
            try
            {
                var dtos = new List<TrademarkResponseDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT
  t.*,
  c.id AS customer_id,
  l.code AS location_code
FROM trademarks t
JOIN customers c ON c.code = t.customer_code
LEFT JOIN locations l ON l.id = t.location_id
WHERE t.is_active=@is_active
ORDER BY t.id DESC;");
                    db.AddParameter(cmd, "is_active", DbTypes.Types.Boolean).Value = isActive;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            var t = ReadTrademark(r);
                            t.Customer = new Customer { Id = r.GetInt32(r.GetOrdinal("customer_id")) };
                            t.Location = new Location { Id = t.LocationId, Code = r.IsDBNull(r.GetOrdinal("location_code")) ? "" : r.GetString(r.GetOrdinal("location_code")) };
                            dtos.Add(Map(t));
                        }
                    }
                }
                return new ApiResponse<List<TrademarkResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<TrademarkResponseDto>>> GetTrademarksByCustomerCode(
            string customerCode,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                int cid = 0;
                string? err = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var res = await ResolveCustomerLinkAsync(db, 0, customerCode);
                    cid = res.CustomerId;
                    err = res.Error;
                }
                if (err != null)
                    return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = err };
                return await GetTrademarksByCustomer(cid, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<TrademarkResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> CreateTrademark(CreateTrademarkDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var (custCode, custId, cErr) = await ResolveCustomerLinkAsync(db, dto.CustomerId, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<TrademarkResponseDto> { Success = false, Message = cErr };
                    var (locationId, lErr) = await ResolveRequiredLocationIdAsync(db, custCode, dto.LocationId, dto.LocationCode);
                    if (lErr != null)
                        return new ApiResponse<TrademarkResponseDto> { Success = false, Message = lErr };

                    var cmd = db.GetCommand(@"
INSERT INTO trademarks (
    customer_id, customer_code, location_id, reg_name, gst_number, pincode,
    city_id, state_id, country_id, address_line1, address_line2,
    contact_persons, emails, mobiles, tier_id, shop_size_id,
    registration_number, category, description, registration_date, expiry_date,
    is_enabled, is_active, remarks, created_at, created_by, modified_at, modified_by
)
VALUES (
    @customer_id, @customer_code, @location_id, @reg_name, @gst_number, @pincode,
    @city_id, @state_id, @country_id, @address_line1, @address_line2,
    @contact_persons, @emails, @mobiles, @tier_id, @shop_size_id,
    @registration_number, @category, @description, @registration_date, @expiry_date,
    @is_enabled, @is_active, @remarks, @created_at, @created_by, @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = custId;
                    db.AddParameter(cmd, "customer_code", DbTypes.Types.String).Value = custCode;
                    db.AddParameter(cmd, "location_id", DbTypes.Types.Integer).Value = locationId;
                    db.AddParameter(cmd, "reg_name", DbTypes.Types.String).Value = dto.RegName;
                    db.AddParameter(cmd, "gst_number", DbTypes.Types.String).Value = dto.GstNumber;
                    db.AddParameter(cmd, "pincode", DbTypes.Types.String).Value = dto.Pincode;
                    db.AddParameter(cmd, "city_id", DbTypes.Types.Integer).Value = dto.CityId;
                    db.AddParameter(cmd, "state_id", DbTypes.Types.Integer).Value = dto.StateId;
                    db.AddParameter(cmd, "country_id", DbTypes.Types.Integer).Value = dto.CountryId.HasValue ? dto.CountryId.Value : DBNull.Value;
                    db.AddParameter(cmd, "address_line1", DbTypes.Types.String).Value = dto.AddressLine1;
                    db.AddParameter(cmd, "address_line2", DbTypes.Types.String).Value = dto.AddressLine2 ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(dto.ContactPersons);
                    db.AddParameter(cmd, "emails", DbTypes.Types.String).Value = JoinListToCsv(dto.Emails);
                    db.AddParameter(cmd, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(dto.Mobiles);
                    db.AddParameter(cmd, "tier_id", DbTypes.Types.Integer).Value = dto.TierId;
                    db.AddParameter(cmd, "shop_size_id", DbTypes.Types.Integer).Value = dto.ShopSizeId.HasValue ? dto.ShopSizeId.Value : DBNull.Value;
                    db.AddParameter(cmd, "registration_number", DbTypes.Types.String).Value = dto.RegistrationNumber ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "category", DbTypes.Types.String).Value = dto.Category ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "description", DbTypes.Types.String).Value = dto.Description ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "registration_date", DbTypes.Types.DateTime).Value = dto.RegistrationDate.HasValue ? dto.RegistrationDate.Value : DBNull.Value;
                    db.AddParameter(cmd, "expiry_date", DbTypes.Types.DateTime).Value = dto.ExpiryDate.HasValue ? dto.ExpiryDate.Value : DBNull.Value;
                    db.AddParameter(cmd, "is_enabled", DbTypes.Types.Boolean).Value = dto.IsEnabled ?? true;
                    db.AddParameter(cmd, "is_active", DbTypes.Types.Boolean).Value = dto.IsActive;
                    db.AddParameter(cmd, "remarks", DbTypes.Types.String).Value = dto.Remarks ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    int newId = 0;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (await r.ReadAsync())
                            newId = r.GetInt32(r.GetOrdinal("id"));
                    }

                    // Return in same shape as before (with CustomerId and LocationCode).
                    var tm = new Trademark
                    {
                        Id = newId,
                        CustomerId = custId,
                        CustomerCode = custCode,
                        LocationId = locationId,
                        RegName = dto.RegName,
                        GstNumber = dto.GstNumber,
                        Pincode = dto.Pincode,
                        CityId = dto.CityId,
                        StateId = dto.StateId,
                        CountryId = dto.CountryId,
                        AddressLine1 = dto.AddressLine1,
                        AddressLine2 = dto.AddressLine2,
                        ContactPersons = dto.ContactPersons,
                        Emails = dto.Emails,
                        Mobiles = dto.Mobiles,
                        TierId = dto.TierId,
                        ShopSizeId = dto.ShopSizeId,
                        RegistrationNumber = dto.RegistrationNumber,
                        Category = dto.Category,
                        Description = dto.Description,
                        RegistrationDate = dto.RegistrationDate,
                        ExpiryDate = dto.ExpiryDate,
                        IsEnabled = dto.IsEnabled ?? true,
                        IsActive = dto.IsActive,
                        Remarks = dto.Remarks,
                        CreatedAt = now,
                        CreatedBy = AuditUserIds.System,
                        ModifiedAt = now,
                        ModifiedBy = AuditUserIds.System,
                        Customer = new Customer { Id = custId }
                    };

                    var lc = "";
                    var lcmd = db.GetCommand("SELECT code FROM locations WHERE id=@id LIMIT 1;");
                    db.AddParameter(lcmd, "id", DbTypes.Types.Integer).Value = locationId;
                    using (DbDataReader r2 = await db.Execute(lcmd))
                    {
                        if (await r2.ReadAsync())
                            lc = r2.IsDBNull(r2.GetOrdinal("code")) ? "" : r2.GetString(r2.GetOrdinal("code"));
                    }
                    tm.Location = new Location { Id = locationId, Code = lc };
                    return new ApiResponse<TrademarkResponseDto> { Success = true, Data = Map(tm) };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<TrademarkResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<TrademarkResponseDto>> UpdateTrademark(int id, UpdateTrademarkDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    Trademark? t = null;
                    var load = db.GetCommand("SELECT * FROM trademarks WHERE id=@id LIMIT 1;");
                    db.AddParameter(load, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r0 = await db.Execute(load))
                    {
                        if (await r0.ReadAsync())
                            t = ReadTrademark(r0);
                    }
                    if (t == null) return new ApiResponse<TrademarkResponseDto> { Success = false, Message = "Trademark not found" };

                    int customerId = 0;
                    if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                    {
                        var res = await ResolveCustomerLinkAsync(db, 0, dto.CustomerCode);
                        if (res.Error != null)
                            return new ApiResponse<TrademarkResponseDto> { Success = false, Message = res.Error };
                        t.CustomerCode = res.CustomerCode;
                        customerId = res.CustomerId;
                    }
                    else
                    {
                        var res = await ResolveCustomerLinkAsync(db, dto.CustomerId, null);
                        if (res.Error != null)
                            return new ApiResponse<TrademarkResponseDto> { Success = false, Message = res.Error };
                        t.CustomerCode = res.CustomerCode;
                        customerId = res.CustomerId;
                    }

                    t.CustomerId = customerId > 0 ? customerId : t.CustomerId;

                    if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                    {
                        var (lid, lErr) = await ResolveRequiredLocationIdAsync(db, t.CustomerCode, 0, dto.LocationCode);
                        if (lErr != null)
                            return new ApiResponse<TrademarkResponseDto> { Success = false, Message = lErr };
                        t.LocationId = lid;
                    }
                    else
                    {
                        t.LocationId = dto.LocationId;
                    }

                    t.RegName = dto.RegName;
                    t.GstNumber = dto.GstNumber;
                    t.Pincode = dto.Pincode;
                    t.CityId = dto.CityId;
                    t.StateId = dto.StateId;
                    t.CountryId = dto.CountryId;
                    t.AddressLine1 = dto.AddressLine1;
                    t.AddressLine2 = dto.AddressLine2;
                    t.ContactPersons = dto.ContactPersons;
                    t.Emails = dto.Emails;
                    t.Mobiles = dto.Mobiles;
                    t.TierId = dto.TierId;
                    t.ShopSizeId = dto.ShopSizeId;
                    t.RegistrationNumber = dto.RegistrationNumber;
                    t.Category = dto.Category;
                    t.Description = dto.Description;
                    t.RegistrationDate = dto.RegistrationDate;
                    t.ExpiryDate = dto.ExpiryDate;
                    t.IsActive = dto.IsActive;
                    if (dto.IsEnabled.HasValue) t.IsEnabled = dto.IsEnabled.Value;
                    t.Remarks = dto.Remarks;
                    t.ModifiedAt = DateTime.UtcNow;
                    t.ModifiedBy = AuditUserIds.System;

                    var upd = db.GetCommand(@"
UPDATE trademarks SET
    customer_id=@customer_id,
    customer_code=@customer_code,
    location_id=@location_id,
    reg_name=@reg_name,
    gst_number=@gst_number,
    pincode=@pincode,
    city_id=@city_id,
    state_id=@state_id,
    country_id=@country_id,
    address_line1=@address_line1,
    address_line2=@address_line2,
    contact_persons=@contact_persons,
    emails=@emails,
    mobiles=@mobiles,
    tier_id=@tier_id,
    shop_size_id=@shop_size_id,
    registration_number=@registration_number,
    category=@category,
    description=@description,
    registration_date=@registration_date,
    expiry_date=@expiry_date,
    is_enabled=@is_enabled,
    is_active=@is_active,
    remarks=@remarks,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                    db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = t.Id;
                    db.AddParameter(upd, "customer_id", DbTypes.Types.Integer).Value = t.CustomerId;
                    db.AddParameter(upd, "customer_code", DbTypes.Types.String).Value = t.CustomerCode;
                    db.AddParameter(upd, "location_id", DbTypes.Types.Integer).Value = t.LocationId;
                    db.AddParameter(upd, "reg_name", DbTypes.Types.String).Value = t.RegName;
                    db.AddParameter(upd, "gst_number", DbTypes.Types.String).Value = t.GstNumber;
                    db.AddParameter(upd, "pincode", DbTypes.Types.String).Value = t.Pincode;
                    db.AddParameter(upd, "city_id", DbTypes.Types.Integer).Value = t.CityId;
                    db.AddParameter(upd, "state_id", DbTypes.Types.Integer).Value = t.StateId;
                    db.AddParameter(upd, "country_id", DbTypes.Types.Integer).Value = t.CountryId.HasValue ? t.CountryId.Value : DBNull.Value;
                    db.AddParameter(upd, "address_line1", DbTypes.Types.String).Value = t.AddressLine1;
                    db.AddParameter(upd, "address_line2", DbTypes.Types.String).Value = t.AddressLine2 ?? (object)DBNull.Value;
                    db.AddParameter(upd, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(t.ContactPersons);
                    db.AddParameter(upd, "emails", DbTypes.Types.String).Value = JoinListToCsv(t.Emails);
                    db.AddParameter(upd, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(t.Mobiles);
                    db.AddParameter(upd, "tier_id", DbTypes.Types.Integer).Value = t.TierId;
                    db.AddParameter(upd, "shop_size_id", DbTypes.Types.Integer).Value = t.ShopSizeId.HasValue ? t.ShopSizeId.Value : DBNull.Value;
                    db.AddParameter(upd, "registration_number", DbTypes.Types.String).Value = t.RegistrationNumber ?? (object)DBNull.Value;
                    db.AddParameter(upd, "category", DbTypes.Types.String).Value = t.Category ?? (object)DBNull.Value;
                    db.AddParameter(upd, "description", DbTypes.Types.String).Value = t.Description ?? (object)DBNull.Value;
                    db.AddParameter(upd, "registration_date", DbTypes.Types.DateTime).Value = t.RegistrationDate.HasValue ? t.RegistrationDate.Value : DBNull.Value;
                    db.AddParameter(upd, "expiry_date", DbTypes.Types.DateTime).Value = t.ExpiryDate.HasValue ? t.ExpiryDate.Value : DBNull.Value;
                    db.AddParameter(upd, "is_enabled", DbTypes.Types.Boolean).Value = t.IsEnabled;
                    db.AddParameter(upd, "is_active", DbTypes.Types.Boolean).Value = t.IsActive;
                    db.AddParameter(upd, "remarks", DbTypes.Types.String).Value = t.Remarks ?? (object)DBNull.Value;
                    db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = t.ModifiedAt;
                    db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = t.ModifiedBy ?? (object)DBNull.Value;
                    await db.ExecuteNonQuery(upd);

                    t.Customer = new Customer { Id = customerId };
                    var lc = "";
                    var lcmd = db.GetCommand("SELECT code FROM locations WHERE id=@id LIMIT 1;");
                    db.AddParameter(lcmd, "id", DbTypes.Types.Integer).Value = t.LocationId;
                    using (DbDataReader r2 = await db.Execute(lcmd))
                    {
                        if (await r2.ReadAsync())
                            lc = r2.IsDBNull(r2.GetOrdinal("code")) ? "" : r2.GetString(r2.GetOrdinal("code"));
                    }
                    t.Location = new Location { Id = t.LocationId, Code = lc };

                    return new ApiResponse<TrademarkResponseDto> { Success = true, Data = Map(t) };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<TrademarkResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteTrademark(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE trademarks
SET is_active=false,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id
RETURNING id;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = DateTime.UtcNow;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (!await r.ReadAsync())
                            return new ApiResponse<bool> { Success = false, Message = "Trademark not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
