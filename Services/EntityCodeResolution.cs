using CRM.Server.Data;
using CRM.Server.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Resolves stable business <c>code</c> values; child tables store <c>customer_id</c> → <c>customers.id</c> and <c>customer_code</c> → <c>customers.code</c>.
    /// API DTOs still expose numeric <c>customerId</c> for clients; enrich or Include <see cref="Models.Customer"/> to populate.
    /// </summary>
    public static class EntityCodeResolution
    {
        /// <summary>Returns FK string and numeric id for DTOs.</summary>
        public static async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(
            CrmDbContext db,
            int customerId,
            string? customerCode,
            CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(customerCode))
            {
                var trimmed = customerCode.Trim();
                var row = await db.Customers.AsNoTracking()
                    .Where(c => c.Code == trimmed)
                    .Select(c => new { c.Id, c.Code })
                    .FirstOrDefaultAsync(ct);
                if (row == null)
                    return (string.Empty, 0, $"Unknown customer code: \"{trimmed}\"");
                return (row.Code, row.Id, null);
            }

            if (customerId <= 0)
                return (string.Empty, 0, "Provide customerId or customerCode");

            var byId = await db.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => new { c.Id, c.Code })
                .FirstOrDefaultAsync(ct);
            if (byId == null || string.IsNullOrWhiteSpace(byId.Code))
                return (string.Empty, 0, "Customer not found or has no code assigned");
            return (byId.Code, byId.Id, null);
        }

        public static async Task<(int CustomerId, string? Error)> ResolveCustomerIdAsync(
            CrmDbContext db,
            int customerId,
            string? customerCode,
            CancellationToken ct = default)
        {
            var (_, id, err) = await ResolveCustomerLinkAsync(db, customerId, customerCode, ct);
            if (err != null)
                return (0, err);
            return (id, null);
        }

        public static async Task<string?> GetCustomerCodeByIdAsync(
            CrmDbContext db,
            int customerId,
            CancellationToken ct = default)
        {
            if (customerId <= 0)
                return null;
            return await db.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>Optional location: both id and code may be null.</summary>
        public static async Task<(int? LocationId, string? Error)> ResolveOptionalLocationIdAsync(
            CrmDbContext db,
            string customerCode,
            int? locationId,
            string? locationCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(customerCode))
                return (null, "Customer code is required to resolve location");

            var cc = customerCode.Trim();
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                var trimmed = locationCode.Trim();
                var id = await db.Locations.AsNoTracking()
                    .Where(l => l.CustomerCode == cc && l.Code == trimmed)
                    .Select(l => (int?)l.Id)
                    .FirstOrDefaultAsync(ct);
                if (!id.HasValue)
                    return (null, $"Unknown location code \"{trimmed}\" for this customer");
                return (id, null);
            }

            return (locationId, null);
        }

        /// <summary>Required location (tickets, investments, trademarks).</summary>
        public static async Task<(int LocationId, string? Error)> ResolveRequiredLocationIdAsync(
            CrmDbContext db,
            string customerCode,
            int locationId,
            string? locationCode,
            CancellationToken ct = default)
        {
            var (lid, err) = await ResolveOptionalLocationIdAsync(db, customerCode, locationId > 0 ? locationId : null, locationCode, ct);
            if (err != null)
                return (0, err);
            if (!lid.HasValue || lid.Value <= 0)
                return (0, "Provide locationId or locationCode");
            return (lid.Value, null);
        }

        public static async Task<Dictionary<int, string?>> CustomerCodesByIdsAsync(
            CrmDbContext db,
            IEnumerable<int> customerIds,
            CancellationToken ct = default)
        {
            var ids = customerIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, string?>();

            return await db.Customers.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => (string?)c.Code, ct);
        }

        public static async Task<Dictionary<int, string>> LocationCodesByIdsAsync(
            CrmDbContext db,
            IEnumerable<int> locationIds,
            CancellationToken ct = default)
        {
            var ids = locationIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, string>();

            return await db.Locations.AsNoTracking()
                .Where(l => ids.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        }

        public static async Task EnrichServiceDtosAsync(CrmDbContext db, IReadOnlyList<ServiceResponseDto> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var custMap = await CustomerCodesByIdsAsync(db, rows.Select(r => r.CustomerId), ct);
            var locIds = rows.Where(r => r.LocationId is > 0).Select(r => r.LocationId!.Value).ToList();
            var locMap = await LocationCodesByIdsAsync(db, locIds, ct);
            foreach (var r in rows)
            {
                r.CustomerCode = custMap.GetValueOrDefault(r.CustomerId);
                if (r.LocationId is { } lid && locMap.TryGetValue(lid, out var lc))
                    r.LocationCode = lc;
            }
        }

        public static async Task EnrichInvoiceDtosAsync(CrmDbContext db, IReadOnlyList<InvoiceResponseDto> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var custMap = await CustomerCodesByIdsAsync(db, rows.Select(r => r.CustomerId), ct);
            foreach (var r in rows)
                r.CustomerCode = custMap.GetValueOrDefault(r.CustomerId);
        }

        public static async Task EnrichTicketDtosAsync(CrmDbContext db, IReadOnlyList<TicketResponseDto> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var custMap = await CustomerCodesByIdsAsync(db, rows.Select(r => r.CustomerId), ct);
            var locMap = await LocationCodesByIdsAsync(db, rows.Select(r => r.LocationId), ct);
            foreach (var r in rows)
            {
                r.CustomerCode = custMap.GetValueOrDefault(r.CustomerId);
                if (locMap.TryGetValue(r.LocationId, out var lc))
                    r.LocationCode = lc;
            }
        }

        public static async Task EnrichLocationDtosAsync(CrmDbContext db, IReadOnlyList<LocationResponseDto> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var custMap = await CustomerCodesByIdsAsync(db, rows.Select(r => r.CustomerId), ct);
            foreach (var r in rows)
                r.CustomerCode = custMap.GetValueOrDefault(r.CustomerId);
        }

        public static async Task EnrichInvestmentDtosAsync(CrmDbContext db, IReadOnlyList<InvestmentResponseDto> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var custMap = await CustomerCodesByIdsAsync(db, rows.Select(r => r.CustomerId), ct);
            var locMap = await LocationCodesByIdsAsync(db, rows.Select(r => r.LocationId), ct);
            foreach (var r in rows)
            {
                r.CustomerCode = custMap.GetValueOrDefault(r.CustomerId);
                if (locMap.TryGetValue(r.LocationId, out var lc))
                    r.LocationCode = lc;
            }
        }

        public static async Task EnrichTrademarkDtosAsync(CrmDbContext db, IReadOnlyList<TrademarkResponseDto> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var custMap = await CustomerCodesByIdsAsync(db, rows.Select(r => r.CustomerId), ct);
            var locMap = await LocationCodesByIdsAsync(db, rows.Select(r => r.LocationId), ct);
            foreach (var r in rows)
            {
                r.CustomerCode = custMap.GetValueOrDefault(r.CustomerId);
                if (locMap.TryGetValue(r.LocationId, out var lc))
                    r.LocationCode = lc;
            }
        }

        public static async Task EnrichCustomerTimelineDtosAsync(
            CrmDbContext db,
            int customerId,
            IReadOnlyList<CustomerTimelineEntryDto> rows,
            CancellationToken ct = default)
        {
            if (rows.Count == 0) return;
            var code = await db.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(ct);
            foreach (var r in rows)
                r.CustomerCode = code;
        }
    }
}
