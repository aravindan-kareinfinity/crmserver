using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>BranchService.ts</c> → <c>LocationService</c>.</summary>
    public interface ILocationService
    {
        Task<ApiResponse<List<LocationResponseDto>>> GetAll();
        Task<ApiResponse<LocationResponseDto>> GetById(int id);
        Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomerCode(string customerCode, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerId(int customerId);
        Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerCode(string customerCode);
        Task<ApiResponse<LocationResponseDto>> CreateLocation(CreateLocationDto dto);
        Task<ApiResponse<LocationResponseDto>> UpdateLocation(int id, CreateLocationDto dto);
        Task<ApiResponse<bool>> DeleteLocation(int id);
        Task<ApiResponse<List<LocationTimelineEntryDto>>> GetTimeline(int locationId);
    }

    public class LocationService : ILocationService
    {
        IDbProvider dbprovider;

        public LocationService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private static LocationResponseDto Map(Location b, int customerId) => new()
        {
            Id = b.Id,
            CustomerId = customerId,
            CustomerCode = b.CustomerCode,
            Code = b.Code,
            Name = b.Name,
            RegName = b.RegName,
            Pincode = b.Pincode,
            CityId = b.CityId,
            StateId = b.StateId,
            CountryId = b.CountryId,
            AddressLine1 = b.AddressLine1,
            AddressLine2 = b.AddressLine2,
            ContactPersons = b.ContactPersons,
            Emails = b.Emails,
            Mobiles = b.Mobiles,
            ShopSizeId = b.ShopSizeId,
            TierId = b.TierId,
            IsPrimary = b.IsPrimary,
            GstNumber = b.GstNumber,
            IsEnabled = b.IsEnabled,
            IsActive = b.IsActive,
            CreatedAt = b.CreatedAt,
            CreatedBy = b.CreatedBy,
            ModifiedAt = b.ModifiedAt,
            ModifiedBy = b.ModifiedBy
        };

        private static List<string> SplitCsvToList(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }

        private static string JoinListToCsv(List<string>? list) =>
            list == null ? string.Empty : string.Join(",", list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));

        private static Location ReadLocation(DbDataReader r)
        {
            return new Location
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                CustomerId = r.GetInt32(r.GetOrdinal("customer_id")),
                CustomerCode = r.GetString(r.GetOrdinal("customer_code")),
                Code = r.GetString(r.GetOrdinal("code")),
                Name = r.GetString(r.GetOrdinal("name")),
                RegName = r.GetString(r.GetOrdinal("reg_name")),
                Pincode = r.GetString(r.GetOrdinal("pincode")),
                CityId = r.GetInt32(r.GetOrdinal("city_id")),
                StateId = r.GetInt32(r.GetOrdinal("state_id")),
                CountryId = r.GetInt32(r.GetOrdinal("country_id")),
                AddressLine1 = r.GetString(r.GetOrdinal("address_line1")),
                AddressLine2 = r.GetString(r.GetOrdinal("address_line2")),
                ContactPersons = SplitCsvToList(r.GetString(r.GetOrdinal("contact_persons"))),
                Emails = SplitCsvToList(r.GetString(r.GetOrdinal("emails"))),
                Mobiles = SplitCsvToList(r.GetString(r.GetOrdinal("mobiles"))),
                ShopSizeId = r.GetInt32(r.GetOrdinal("shop_size_id")),
                TierId = r.GetInt32(r.GetOrdinal("tier_id")),
                IsPrimary = r.GetBoolean(r.GetOrdinal("is_primary")),
                GstNumber = r.GetString(r.GetOrdinal("gst_number")),
                IsEnabled = r.GetBoolean(r.GetOrdinal("is_enabled")),
                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
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

        private async Task<(string CustomerCode, int CustomerId, string? Error)> ResolveCustomerLinkAsync(
            IDb db,
            int customerId,
            string? customerCode)
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

        public async Task<ApiResponse<List<LocationResponseDto>>> GetAll()
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM locations
ORDER BY id DESC;");
                    var dtos = new List<LocationResponseDto>();
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            var loc = ReadLocation(r);
                            dtos.Add(Map(loc, loc.CustomerId));
                        }
                    }
                    return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = dtos };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<LocationResponseDto>> GetById(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM locations
WHERE id=@id
LIMIT 1;");
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        if (!await r.ReadAsync())
                            return new ApiResponse<LocationResponseDto> { Success = false, Message = "Location not found" };
                        var loc = ReadLocation(r);
                        return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(loc, loc.CustomerId) };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<LocationResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                using (IDb db0 = await dbprovider.GetDb())
                {
                    await db0.Connect();
                    var chk = db0.GetCommand("SELECT 1 FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
                    db0.AddParameter(chk, "id", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader cr = await db0.Execute(chk))
                    {
                        if (!await cr.ReadAsync())
                            return new ApiResponse<PaginatedResponse<LocationResponseDto>>
                            {
                                Success = true,
                                Data = new PaginatedResponse<LocationResponseDto>
                                {
                                    Items = new List<LocationResponseDto>(),
                                    Total = 0,
                                    PageNumber = pageNumber,
                                    PageSize = pageSize
                                }
                            };
                    }
                }
                var offset = Math.Max(0, (pageNumber - 1) * pageSize);
                int total = 0;
                var dtos = new List<LocationResponseDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var countCmd = db.GetCommand("SELECT COUNT(*)::int AS total FROM locations WHERE customer_id=@cid;");
                    db.AddParameter(countCmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader r = await db.Execute(countCmd))
                    {
                        if (await r.ReadAsync()) total = r.GetInt32(r.GetOrdinal("total"));
                    }

                    var cmd = db.GetCommand(@"
SELECT *
FROM locations
WHERE customer_id=@cid
ORDER BY id DESC
LIMIT @limit OFFSET @offset;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    db.AddParameter(cmd, "limit", DbTypes.Types.Integer).Value = pageSize;
                    db.AddParameter(cmd, "offset", DbTypes.Types.Integer).Value = offset;
                    using (DbDataReader r2 = await db.Execute(cmd))
                    {
                        while (await r2.ReadAsync())
                        {
                            var loc = ReadLocation(r2);
                            dtos.Add(Map(loc, loc.CustomerId));
                        }
                    }
                }
                return new ApiResponse<PaginatedResponse<LocationResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<LocationResponseDto>
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
                return new ApiResponse<PaginatedResponse<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<LocationResponseDto>>> GetLocationsByCustomerCode(
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
                    return new ApiResponse<PaginatedResponse<LocationResponseDto>> { Success = false, Message = err };
                return await GetLocationsByCustomer(cid, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerId(int customerId)
        {
            try
            {
                using (IDb db0 = await dbprovider.GetDb())
                {
                    await db0.Connect();
                    var chk = db0.GetCommand("SELECT 1 FROM customers WHERE id=@id AND is_active=true LIMIT 1;");
                    db0.AddParameter(chk, "id", DbTypes.Types.Integer).Value = customerId;
                    using (DbDataReader cr = await db0.Execute(chk))
                    {
                        if (!await cr.ReadAsync())
                            return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = new List<LocationResponseDto>() };
                    }
                }
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT *
FROM locations
WHERE customer_id=@cid
ORDER BY id DESC;");
                    db.AddParameter(cmd, "cid", DbTypes.Types.Integer).Value = customerId;
                    var dtos = new List<LocationResponseDto>();
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            var loc = ReadLocation(r);
                            dtos.Add(Map(loc, loc.CustomerId));
                        }
                    }
                    return new ApiResponse<List<LocationResponseDto>> { Success = true, Data = dtos };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<LocationResponseDto>>> GetByCustomerCode(string customerCode)
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
                    return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = err };
                return await GetByCustomerId(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<LocationResponseDto>> CreateLocation(CreateLocationDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                var code = string.IsNullOrWhiteSpace(dto.Code)
                    ? $"LOC-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
                    : dto.Code;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var (custCode, custId, cErr) = await ResolveCustomerLinkAsync(db, dto.CustomerId, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<LocationResponseDto> { Success = false, Message = cErr };

                    var cmd = db.GetCommand(@"
INSERT INTO locations (
    customer_id, customer_code, code, name, reg_name,
    pincode, city_id, state_id, country_id,
    address_line1, address_line2,
    contact_persons, emails, mobiles,
    shop_size_id, tier_id,
    is_primary, gst_number, is_enabled, is_active,
    created_at, created_by,
    modified_at, modified_by
)
VALUES (
    @customer_id, @customer_code, @code, @name, @reg_name,
    @pincode, @city_id, @state_id, @country_id,
    @address_line1, @address_line2,
    @contact_persons, @emails, @mobiles,
    @shop_size_id, @tier_id,
    @is_primary, @gst_number, @is_enabled, true,
    @created_at, @created_by,
    @modified_at, @modified_by
)
RETURNING id;");
                    db.AddParameter(cmd, "customer_id", DbTypes.Types.Integer).Value = custId;
                    db.AddParameter(cmd, "customer_code", DbTypes.Types.String).Value = custCode;
                    db.AddParameter(cmd, "code", DbTypes.Types.String).Value = code;
                    db.AddParameter(cmd, "name", DbTypes.Types.String).Value = dto.Name;
                    db.AddParameter(cmd, "reg_name", DbTypes.Types.String).Value = dto.RegName;
                    db.AddParameter(cmd, "pincode", DbTypes.Types.String).Value = dto.Pincode;
                    db.AddParameter(cmd, "city_id", DbTypes.Types.Integer).Value = dto.CityId;
                    db.AddParameter(cmd, "state_id", DbTypes.Types.Integer).Value = dto.StateId;
                    db.AddParameter(cmd, "country_id", DbTypes.Types.Integer).Value = dto.CountryId;
                    db.AddParameter(cmd, "address_line1", DbTypes.Types.String).Value = dto.AddressLine1;
                    db.AddParameter(cmd, "address_line2", DbTypes.Types.String).Value = dto.AddressLine2;
                    db.AddParameter(cmd, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(dto.ContactPersons);
                    db.AddParameter(cmd, "emails", DbTypes.Types.String).Value = JoinListToCsv(dto.Emails);
                    db.AddParameter(cmd, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(dto.Mobiles);
                    db.AddParameter(cmd, "shop_size_id", DbTypes.Types.Integer).Value = dto.ShopSizeId;
                    db.AddParameter(cmd, "tier_id", DbTypes.Types.Integer).Value = dto.TierId;
                    db.AddParameter(cmd, "is_primary", DbTypes.Types.Boolean).Value = dto.IsPrimary;
                    db.AddParameter(cmd, "gst_number", DbTypes.Types.String).Value = dto.GstNumber;
                    db.AddParameter(cmd, "is_enabled", DbTypes.Types.Boolean).Value = dto.IsEnabled ?? true;
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

                    var loc = new Location
                    {
                        Id = newId,
                        CustomerId = custId,
                        CustomerCode = custCode,
                        Name = dto.Name,
                        RegName = dto.RegName,
                        Code = code,
                        Pincode = dto.Pincode,
                        CityId = dto.CityId,
                        StateId = dto.StateId,
                        CountryId = dto.CountryId,
                        AddressLine1 = dto.AddressLine1,
                        AddressLine2 = dto.AddressLine2,
                        ContactPersons = dto.ContactPersons,
                        Emails = dto.Emails,
                        Mobiles = dto.Mobiles,
                        ShopSizeId = dto.ShopSizeId,
                        TierId = dto.TierId,
                        IsPrimary = dto.IsPrimary,
                        GstNumber = dto.GstNumber,
                        IsEnabled = dto.IsEnabled ?? true,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = AuditUserIds.System,
                        ModifiedAt = now,
                        ModifiedBy = AuditUserIds.System
                    };

                    return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(loc, loc.CustomerId) };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<LocationResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<LocationResponseDto>> UpdateLocation(int id, CreateLocationDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    // Load existing row
                    Location? location = null;
                    var load = db.GetCommand("SELECT * FROM locations WHERE id=@id LIMIT 1;");
                    db.AddParameter(load, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader r0 = await db.Execute(load))
                    {
                        if (await r0.ReadAsync())
                            location = ReadLocation(r0);
                    }
                    if (location == null) return new ApiResponse<LocationResponseDto> { Success = false, Message = "Location not found" };

                    if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                    {
                        var res = await ResolveCustomerLinkAsync(db, 0, dto.CustomerCode);
                        if (res.Error != null)
                            return new ApiResponse<LocationResponseDto> { Success = false, Message = res.Error };
                        location.CustomerCode = res.CustomerCode;
                        location.CustomerId = res.CustomerId;
                    }
                    else if (dto.CustomerId > 0)
                    {
                        var res = await ResolveCustomerLinkAsync(db, dto.CustomerId, null);
                        if (res.Error != null)
                            return new ApiResponse<LocationResponseDto> { Success = false, Message = res.Error };
                        location.CustomerCode = res.CustomerCode;
                        location.CustomerId = res.CustomerId;
                    }
                    else
                    {
                        var res = await ResolveCustomerLinkAsync(db, 0, location.CustomerCode);
                        if (res.Error == null)
                            location.CustomerId = res.CustomerId;
                    }

                    location.Name = dto.Name;
                    location.RegName = dto.RegName;
                    if (!string.IsNullOrWhiteSpace(dto.Code)) location.Code = dto.Code;
                    location.AddressLine1 = dto.AddressLine1;
                    location.AddressLine2 = dto.AddressLine2;
                    location.Pincode = dto.Pincode;
                    location.GstNumber = dto.GstNumber;
                    location.CityId = dto.CityId;
                    location.StateId = dto.StateId;
                    location.CountryId = dto.CountryId;
                    location.ContactPersons = dto.ContactPersons;
                    location.Emails = dto.Emails;
                    location.Mobiles = dto.Mobiles;
                    location.ShopSizeId = dto.ShopSizeId;
                    location.TierId = dto.TierId;
                    location.IsPrimary = dto.IsPrimary;
                    if (dto.IsEnabled.HasValue) location.IsEnabled = dto.IsEnabled.Value;
                    location.ModifiedAt = DateTime.UtcNow;
                    location.ModifiedBy = AuditUserIds.System;

                    var upd = db.GetCommand(@"
UPDATE locations SET
    customer_id=@customer_id,
    customer_code=@customer_code,
    code=@code,
    name=@name,
    reg_name=@reg_name,
    pincode=@pincode,
    city_id=@city_id,
    state_id=@state_id,
    country_id=@country_id,
    address_line1=@address_line1,
    address_line2=@address_line2,
    contact_persons=@contact_persons,
    emails=@emails,
    mobiles=@mobiles,
    shop_size_id=@shop_size_id,
    tier_id=@tier_id,
    is_primary=@is_primary,
    gst_number=@gst_number,
    is_enabled=@is_enabled,
    modified_at=@modified_at,
    modified_by=@modified_by
WHERE id=@id;");
                    db.AddParameter(upd, "id", DbTypes.Types.Integer).Value = location.Id;
                    db.AddParameter(upd, "customer_id", DbTypes.Types.Integer).Value = location.CustomerId;
                    db.AddParameter(upd, "customer_code", DbTypes.Types.String).Value = location.CustomerCode;
                    db.AddParameter(upd, "code", DbTypes.Types.String).Value = location.Code;
                    db.AddParameter(upd, "name", DbTypes.Types.String).Value = location.Name;
                    db.AddParameter(upd, "reg_name", DbTypes.Types.String).Value = location.RegName;
                    db.AddParameter(upd, "pincode", DbTypes.Types.String).Value = location.Pincode;
                    db.AddParameter(upd, "city_id", DbTypes.Types.Integer).Value = location.CityId;
                    db.AddParameter(upd, "state_id", DbTypes.Types.Integer).Value = location.StateId;
                    db.AddParameter(upd, "country_id", DbTypes.Types.Integer).Value = location.CountryId;
                    db.AddParameter(upd, "address_line1", DbTypes.Types.String).Value = location.AddressLine1;
                    db.AddParameter(upd, "address_line2", DbTypes.Types.String).Value = location.AddressLine2;
                    db.AddParameter(upd, "contact_persons", DbTypes.Types.String).Value = JoinListToCsv(location.ContactPersons);
                    db.AddParameter(upd, "emails", DbTypes.Types.String).Value = JoinListToCsv(location.Emails);
                    db.AddParameter(upd, "mobiles", DbTypes.Types.String).Value = JoinListToCsv(location.Mobiles);
                    db.AddParameter(upd, "shop_size_id", DbTypes.Types.Integer).Value = location.ShopSizeId;
                    db.AddParameter(upd, "tier_id", DbTypes.Types.Integer).Value = location.TierId;
                    db.AddParameter(upd, "is_primary", DbTypes.Types.Boolean).Value = location.IsPrimary;
                    db.AddParameter(upd, "gst_number", DbTypes.Types.String).Value = location.GstNumber;
                    db.AddParameter(upd, "is_enabled", DbTypes.Types.Boolean).Value = location.IsEnabled;
                    db.AddParameter(upd, "modified_at", DbTypes.Types.DateTime).Value = location.ModifiedAt;
                    db.AddParameter(upd, "modified_by", DbTypes.Types.Long).Value = location.ModifiedBy ?? (object)DBNull.Value;
                    await db.ExecuteNonQuery(upd);

                    return new ApiResponse<LocationResponseDto> { Success = true, Data = Map(location, location.CustomerId) };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<LocationResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteLocation(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
UPDATE locations
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
                            return new ApiResponse<bool> { Success = false, Message = "Location not found" };
                    }
                    return new ApiResponse<bool> { Success = true, Data = true };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<LocationTimelineEntryDto>>> GetTimeline(int locationId)
        {
            try
            {
                var rows = new List<LocationTimelineEntryDto>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var cmd = db.GetCommand(@"
SELECT id, location_id, type, notes, file_id, file_name, is_active, created_at, created_by, modified_at, modified_by
FROM location_timelines
WHERE location_id=@location_id
ORDER BY id DESC;");
                    db.AddParameter(cmd, "location_id", DbTypes.Types.Integer).Value = locationId;
                    using (DbDataReader r = await db.Execute(cmd))
                    {
                        while (await r.ReadAsync())
                        {
                            rows.Add(new LocationTimelineEntryDto
                            {
                                Id = r.GetInt32(r.GetOrdinal("id")),
                                LocationId = r.GetInt32(r.GetOrdinal("location_id")),
                                Type = r.GetInt32(r.GetOrdinal("type")),
                                Notes = r.GetString(r.GetOrdinal("notes")),
                                FileId = r.IsDBNull(r.GetOrdinal("file_id")) ? null : r.GetInt32(r.GetOrdinal("file_id")),
                                FileName = r.IsDBNull(r.GetOrdinal("file_name")) ? null : r.GetString(r.GetOrdinal("file_name")),
                                IsActive = r.GetBoolean(r.GetOrdinal("is_active")),
                                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
                                CreatedBy = r.GetInt64(r.GetOrdinal("created_by")),
                                ModifiedAt = r.GetDateTime(r.GetOrdinal("modified_at")),
                                ModifiedBy = r.IsDBNull(r.GetOrdinal("modified_by")) ? null : r.GetInt64(r.GetOrdinal("modified_by")),
                            });
                        }
                    }
                }
                return new ApiResponse<List<LocationTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<LocationTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }
    }
}
