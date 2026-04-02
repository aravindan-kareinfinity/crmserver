using System.Data.Common;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>ReferenceService.ts</c>.</summary>
    public interface IReferenceService
    {
        Task<ApiResponse<List<ReferenceResponseDto>>> GetAll();
        Task<ApiResponse<List<ReferenceResponseDto>>> GetReferencesByCategory(string category);
        Task<ApiResponse<ReferenceResponseDto>> GetReferenceById(int id);
        Task<ApiResponse<ReferenceResponseDto>> GetByValue(string value);
        Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelById(int id);
        Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelByValue(string value);
        Task<ApiResponse<ReferenceResponseDto>> Create(CreateReferenceDto dto);
        Task<ApiResponse<ReferenceResponseDto>> Update(int id, UpdateReferenceDto dto);
        Task<ApiResponse<bool>> Delete(int id);
    }

    public class ReferenceService : IReferenceService
    {
        IDbProvider dbprovider;
        IQueryBuilderProvider querybuilderprovider;

        public ReferenceService(IDbProvider dbprovider, IQueryBuilderProvider querybuilderprovider)
        {
            this.dbprovider = dbprovider;
            this.querybuilderprovider = querybuilderprovider;
        }

        private static ReferenceResponseDto Map(ReferenceEntry r) => new()
        {
            Id = r.Id,
            Category = r.Category,
            Label = r.Label,
            Value = r.Value,
            IsActive = r.IsActive,
            SortOrder = r.SortOrder,
            RequiresImplementation = r.RequiresImplementation,
            IsImplementation = r.IsImplementation
        };

        public async Task<ApiResponse<List<ReferenceResponseDto>>> GetAll()
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"
SELECT
    id,
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation
FROM reference_entries
ORDER BY category, sort_order;";

                    var command = db.GetCommand(sql);
                    var list = new List<ReferenceResponseDto>();
                    using (DbDataReader reader = await db.Execute(command))
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ReferenceResponseDto
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

                    return new ApiResponse<List<ReferenceResponseDto>> { Success = true, Data = list };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ReferenceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<ReferenceResponseDto>>> GetReferencesByCategory(string category)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string query = @"
SELECT
    id,
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation
FROM reference_entries";

                    var queryBuilder = querybuilderprovider.GetQueryBuilder(query);
                    queryBuilder.AddParameter("category", "=", "category", category, DbTypes.Types.String);
                    queryBuilder.AddParameter("is_active", "=", "is_active", true, DbTypes.Types.Boolean);
                    queryBuilder.AddOrderBy(QueryBuilder.Order.ASC, "sort_order");

                    var command = queryBuilder.GetCommand(db);
                    var list = new List<ReferenceResponseDto>();
                    using (DbDataReader reader = await db.Execute(command))
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new ReferenceResponseDto
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

                    return new ApiResponse<List<ReferenceResponseDto>> { Success = true, Data = list };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ReferenceResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> GetReferenceById(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string query = @"
SELECT
    id,
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation
FROM reference_entries";

                    var queryBuilder = querybuilderprovider.GetQueryBuilder(query);
                    queryBuilder.AddParameter("id", "=", "id", id, DbTypes.Types.Integer);

                    var command = queryBuilder.GetCommand(db);
                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Reference not found" };

                        return new ApiResponse<ReferenceResponseDto>
                        {
                            Success = true,
                            Data = new ReferenceResponseDto
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
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> GetByValue(string value)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string query = @"
SELECT
    id,
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation
FROM reference_entries";

                    var queryBuilder = querybuilderprovider.GetQueryBuilder(query);
                    queryBuilder.AddParameter("value", "=", "value", value, DbTypes.Types.String);

                    // LIMIT 1 for parity with FirstOrDefaultAsync
                    queryBuilder.AddLimitOffset(1, 0);

                    var command = queryBuilder.GetCommand(db);
                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Reference not found" };

                        return new ApiResponse<ReferenceResponseDto>
                        {
                            Success = true,
                            Data = new ReferenceResponseDto
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
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelById(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"SELECT label FROM reference_entries WHERE id=@id LIMIT 1;";
                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "id", DbTypes.Types.Integer).Value = id;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        var label = id.ToString();
                        if (await reader.ReadAsync() && !reader.IsDBNull(reader.GetOrdinal("label")))
                            label = reader.GetString(reader.GetOrdinal("label"));

                        return new ApiResponse<ReferenceLabelResponseDto>
                        {
                            Success = true,
                            Data = new ReferenceLabelResponseDto { Label = label }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceLabelResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceLabelResponseDto>> GetLabelByValue(string value)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"SELECT label FROM reference_entries WHERE value=@value LIMIT 1;";
                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "value", DbTypes.Types.String).Value = value;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        var label = value;
                        if (await reader.ReadAsync() && !reader.IsDBNull(reader.GetOrdinal("label")))
                            label = reader.GetString(reader.GetOrdinal("label"));

                        return new ApiResponse<ReferenceLabelResponseDto>
                        {
                            Success = true,
                            Data = new ReferenceLabelResponseDto { Label = label }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceLabelResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> Create(CreateReferenceDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"
INSERT INTO reference_entries (
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation
)
VALUES (
    @category,
    @label,
    @value,
    @is_active,
    @sort_order,
    @requires_implementation,
    @is_implementation
)
RETURNING
    id,
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation;";

                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "category", DbTypes.Types.String).Value = dto.Category.Trim();
                    db.AddParameter(command, "label", DbTypes.Types.String).Value = dto.Label.Trim();
                    db.AddParameter(command, "value", DbTypes.Types.String).Value = dto.Value.Trim();
                    db.AddParameter(command, "is_active", DbTypes.Types.Boolean).Value = dto.IsActive;
                    db.AddParameter(command, "sort_order", DbTypes.Types.Integer).Value = dto.SortOrder;
                    db.AddParameter(command, "requires_implementation", DbTypes.Types.Boolean).Value =
                        dto.RequiresImplementation.HasValue ? dto.RequiresImplementation.Value : DBNull.Value;
                    db.AddParameter(command, "is_implementation", DbTypes.Types.Boolean).Value =
                        dto.IsImplementation.HasValue ? dto.IsImplementation.Value : DBNull.Value;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Create failed" };

                        return new ApiResponse<ReferenceResponseDto>
                        {
                            Success = true,
                            Data = new ReferenceResponseDto
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
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<ReferenceResponseDto>> Update(int id, UpdateReferenceDto dto)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"
UPDATE reference_entries SET
    category = @category,
    label = @label,
    value = @value,
    is_active = @is_active,
    sort_order = @sort_order,
    requires_implementation = @requires_implementation,
    is_implementation = @is_implementation
WHERE id = @id
RETURNING
    id,
    category,
    label,
    value,
    is_active,
    sort_order,
    requires_implementation,
    is_implementation;";

                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(command, "category", DbTypes.Types.String).Value = dto.Category.Trim();
                    db.AddParameter(command, "label", DbTypes.Types.String).Value = dto.Label.Trim();
                    db.AddParameter(command, "value", DbTypes.Types.String).Value = dto.Value.Trim();
                    db.AddParameter(command, "is_active", DbTypes.Types.Boolean).Value = dto.IsActive;
                    db.AddParameter(command, "sort_order", DbTypes.Types.Integer).Value = dto.SortOrder;
                    db.AddParameter(command, "requires_implementation", DbTypes.Types.Boolean).Value =
                        dto.RequiresImplementation.HasValue ? dto.RequiresImplementation.Value : DBNull.Value;
                    db.AddParameter(command, "is_implementation", DbTypes.Types.Boolean).Value =
                        dto.IsImplementation.HasValue ? dto.IsImplementation.Value : DBNull.Value;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<ReferenceResponseDto> { Success = false, Message = "Reference not found" };

                        return new ApiResponse<ReferenceResponseDto>
                        {
                            Success = true,
                            Data = new ReferenceResponseDto
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
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReferenceResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"DELETE FROM reference_entries WHERE id=@id RETURNING id;";
                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "id", DbTypes.Types.Integer).Value = id;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<bool> { Success = false, Message = "Reference not found" };

                        return new ApiResponse<bool> { Success = true, Data = true };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
