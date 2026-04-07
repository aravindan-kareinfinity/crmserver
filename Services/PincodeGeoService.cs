using System.Text.Json;
using System.Text.RegularExpressions;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    /// <summary>
    /// Resolves Indian pincodes via <see href="https://api.postalpincode.in/">api.postalpincode.in</see>
    /// and maps District → <c>City</c>, State → <c>State</c>, Country → <c>Country</c> in <c>reference_entries</c>.
    /// </summary>
    public interface IPincodeGeoService
    {
        Task<ApiResponse<PincodeResolveResponseDto>> ResolveAsync(string pincode);
    }

    public class PincodeGeoService : IPincodeGeoService
    {
        private const string HttpClientName = "PostalPincode";
        IHttpClientFactory httpFactory;
        IDbProvider dbprovider;

        public PincodeGeoService(IHttpClientFactory httpFactory, IDbProvider dbprovider)
        {
            this.httpFactory = httpFactory;
            this.dbprovider = dbprovider;
        }

        public async Task<ApiResponse<PincodeResolveResponseDto>> ResolveAsync(string pincode)
        {
            var fail = (string msg) => new ApiResponse<PincodeResolveResponseDto> { Success = false, Message = msg };

            var p = (pincode ?? "").Trim();
            if (!Regex.IsMatch(p, @"^\d{6}$"))
                return fail("Pincode must be exactly 6 digits");

            HttpResponseMessage response;
            try
            {
                var client = httpFactory.CreateClient(HttpClientName);
                response = await client.GetAsync(new Uri($"pincode/{p}", UriKind.Relative));
            }
            catch (Exception ex)
            {
                return fail($"Pincode service unreachable: {ex.Message}");
            }

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json))
                return fail("Empty response from pincode API");

            string district;
            string state;
            string country;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                    return fail("Invalid pincode API response shape");

                var head = root[0];
                var status = GetString(head, "Status");
                if (!string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase))
                    return fail(GetString(head, "Message").Length > 0 ? GetString(head, "Message") : "Pincode not found");

                if (!head.TryGetProperty("PostOffice", out var offices) ||
                    offices.ValueKind != JsonValueKind.Array ||
                    offices.GetArrayLength() == 0)
                    return fail("No post office data for this pincode");

                var office = offices[0];
                district = GetString(office, "District");
                state = GetString(office, "State");
                country = GetString(office, "Country");

                if (string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(country))
                    return fail("Incomplete District/State/Country in API response");
            }
            catch (JsonException ex)
            {
                return fail($"Could not parse pincode API: {ex.Message}");
            }

            var (countryId, c1) = await FindOrCreateReferenceAsync("Country", country);
            var (stateId, c2) = await FindOrCreateReferenceAsync("State", state);
            var (cityId, c3) = await FindOrCreateReferenceAsync("City", district);
            var createdAny = c1 || c2 || c3;

            return new ApiResponse<PincodeResolveResponseDto>
            {
                Success = true,
                Data = new PincodeResolveResponseDto
                {
                    Pincode = p,
                    CountryId = countryId,
                    StateId = stateId,
                    CityId = cityId,
                    Country = country.Trim(),
                    State = state.Trim(),
                    District = district.Trim(),
                    CreatedNewReferences = createdAny
                }
            };
        }

        private static string GetString(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var p))
                return "";
            return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? "").Trim() : "";
        }

        private static string ToReferenceSlug(string displayName)
        {
            var s = displayName.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"[^a-z0-9]+", "_");
            s = s.Trim('_');
            return string.IsNullOrEmpty(s) ? "unknown" : s;
        }

        private async Task<(int Id, bool CreatedNew)> FindOrCreateReferenceAsync(string category, string labelRaw)
        {
            var label = labelRaw.Trim();
            var slug = ToReferenceSlug(label);
            var lowerLabel = label.ToLowerInvariant();
            var lowerSlug = slug.ToLowerInvariant();

            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                await db.BeginTransaction();
                try
                {
                    // 1) Find existing (case-insensitive match on value OR label)
                    string selectSql = @"
SELECT id
FROM reference_entries
WHERE category = @category
  AND is_active = true
  AND (lower(value) = @lower_slug OR lower(label) = @lower_label)
LIMIT 1;";
                    var selectCmd = db.GetCommand(selectSql);
                    db.AddParameter(selectCmd, "category", DbTypes.Types.String).Value = category;
                    db.AddParameter(selectCmd, "lower_slug", DbTypes.Types.String).Value = lowerSlug;
                    db.AddParameter(selectCmd, "lower_label", DbTypes.Types.String).Value = lowerLabel;

                    // Npgsql: cannot Commit while a reader is still open — read, dispose reader, then commit.
                    int? existingId = null;
                    using (DbDataReader reader = await db.Execute(selectCmd))
                    {
                        if (await reader.ReadAsync())
                            existingId = reader.GetInt32(reader.GetOrdinal("id"));
                    }

                    if (existingId.HasValue)
                    {
                        await db.CommitTransaction();
                        return (existingId.Value, false);
                    }

                    // 2) Next sort order
                    string maxSql = @"SELECT COALESCE(MAX(sort_order), 0) AS max_sort FROM reference_entries WHERE category = @category;";
                    var maxCmd = db.GetCommand(maxSql);
                    db.AddParameter(maxCmd, "category", DbTypes.Types.String).Value = category;
                    int maxSort = 0;
                    using (DbDataReader maxReader = await db.Execute(maxCmd))
                    {
                        if (await maxReader.ReadAsync())
                            maxSort = maxReader.GetInt32(maxReader.GetOrdinal("max_sort"));
                    }

                    // 3) Insert
                    string insertSql = @"
INSERT INTO reference_entries (category, label, value, is_active, sort_order)
VALUES (@category, @label, @value, true, @sort_order)
RETURNING id;";
                    var insertCmd = db.GetCommand(insertSql);
                    db.AddParameter(insertCmd, "category", DbTypes.Types.String).Value = category;
                    db.AddParameter(insertCmd, "label", DbTypes.Types.String).Value = label;
                    db.AddParameter(insertCmd, "value", DbTypes.Types.String).Value = slug;
                    db.AddParameter(insertCmd, "sort_order", DbTypes.Types.Integer).Value = maxSort + 1;

                    int newId = 0;
                    using (DbDataReader insReader = await db.Execute(insertCmd))
                    {
                        if (await insReader.ReadAsync())
                            newId = insReader.GetInt32(insReader.GetOrdinal("id"));
                    }

                    await db.CommitTransaction();
                    return (newId, true);
                }
                catch
                {
                    await db.RollbackTransaction();
                    throw;
                }
            }
        }
    }
}
