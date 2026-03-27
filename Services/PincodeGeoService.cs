using System.Text.Json;
using System.Text.RegularExpressions;
using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

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
        private readonly IHttpClientFactory _httpFactory;
        private readonly CrmDbContext _db;

        public PincodeGeoService(IHttpClientFactory httpFactory, CrmDbContext db)
        {
            _httpFactory = httpFactory;
            _db = db;
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
                var client = _httpFactory.CreateClient(HttpClientName);
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

            var existing = await _db.ReferenceEntries
                .Where(r => r.Category == category && r.IsActive)
                .FirstOrDefaultAsync(r =>
                    r.Value.ToLower() == lowerSlug ||
                    r.Label.ToLower() == lowerLabel);

            if (existing != null)
                return (existing.Id, false);

            var maxSort = await _db.ReferenceEntries
                .Where(r => r.Category == category)
                .Select(r => (int?)r.SortOrder)
                .MaxAsync() ?? 0;

            var e = new ReferenceEntry
            {
                Category = category,
                Label = label,
                Value = slug,
                IsActive = true,
                SortOrder = maxSort + 1
            };
            _db.ReferenceEntries.Add(e);
            await _db.SaveChangesAsync();
            return (e.Id, true);
        }
    }
}
