using System.Globalization;
using System.Text.RegularExpressions;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;
using Npgsql;

namespace CRM.Server.Services
{
    public interface IReportService
    {
        Task<ApiResponse<List<ReportResponseDto>>> GetAll();
        Task<ApiResponse<ReportResponseDto>> GetById(int id);
        Task<ApiResponse<ReportResponseDto>> Create(CreateReportDto dto);
        Task<ApiResponse<ReportResponseDto>> Update(int id, UpdateReportDto dto);
        Task<ApiResponse<bool>> Delete(int id);
        Task<ApiResponse<ReportRunResultDto>> Run(int id, RunReportRequestDto dto);
    }

    public class ReportService : IReportService
    {
        IDbProvider dbprovider;
        NpgsqlDataSource dataSource;

        public ReportService(IDbProvider dbprovider, NpgsqlDataSource dataSource)
        {
            this.dbprovider = dbprovider;
            this.dataSource = dataSource;
        }

        private static ReportResponseDto MapReport(Report r) => new()
        {
            Id = r.Id,
            Name = r.Name,
            Module = r.Module,
            Columns = r.Columns ?? new List<string>(),
            Filters = r.Filters ?? new Dictionary<string, string>(),
            GroupBy = r.GroupBy,
            SortBy = r.SortBy,
            Query = r.Query,
            IsActive = r.IsActive,
            CreatedBy = r.CreatedBy,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt,
            ModifiedBy = r.ModifiedBy,
            LastRun = r.LastRun,
        };

        public async Task<ApiResponse<List<ReportResponseDto>>> GetAll()
        {
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                string sql = @"
SELECT
    id,
    name,
    module,
    columns,
    filters,
    group_by,
    sort_by,
    query,
    is_active,
    created_by,
    created_at,
    modified_at,
    modified_by,
    last_run
FROM reports
WHERE is_active = true
ORDER BY id DESC;";

                var cmd = db.GetCommand(sql);
                var outRows = new List<ReportResponseDto>();
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    while (await reader.ReadAsync())
                    {
                        var colsCsv = reader.IsDBNull(reader.GetOrdinal("columns")) ? "" : reader.GetString(reader.GetOrdinal("columns"));
                        var filtersJson = reader.IsDBNull(reader.GetOrdinal("filters")) ? "" : reader.GetString(reader.GetOrdinal("filters"));
                        outRows.Add(new ReportResponseDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Name = reader.GetString(reader.GetOrdinal("name")),
                            Module = reader.GetString(reader.GetOrdinal("module")),
                            Columns = string.IsNullOrWhiteSpace(colsCsv) ? new List<string>() : colsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                            Filters = string.IsNullOrWhiteSpace(filtersJson)
                                ? new Dictionary<string, string>()
                                : (System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(filtersJson) ?? new Dictionary<string, string>()),
                            GroupBy = reader.IsDBNull(reader.GetOrdinal("group_by")) ? null : reader.GetString(reader.GetOrdinal("group_by")),
                            SortBy = reader.IsDBNull(reader.GetOrdinal("sort_by")) ? null : reader.GetString(reader.GetOrdinal("sort_by")),
                            Query = reader.IsDBNull(reader.GetOrdinal("query")) ? null : reader.GetString(reader.GetOrdinal("query")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? AuditUserIds.System : reader.GetInt64(reader.GetOrdinal("created_by")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                            ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                            ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            LastRun = reader.GetDateTime(reader.GetOrdinal("last_run")),
                        });
                    }
                }

                return new ApiResponse<List<ReportResponseDto>> { Success = true, Data = outRows };
            }
        }

        public async Task<ApiResponse<ReportResponseDto>> GetById(int id)
        {
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                string sql = @"
SELECT
    id,
    name,
    module,
    columns,
    filters,
    group_by,
    sort_by,
    query,
    is_active,
    created_by,
    created_at,
    modified_at,
    modified_by,
    last_run
FROM reports
WHERE id = @id AND is_active = true
LIMIT 1;";

                var cmd = db.GetCommand(sql);
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (!await reader.ReadAsync())
                        return new ApiResponse<ReportResponseDto> { Success = false, Message = "Report not found" };

                    var colsCsv = reader.IsDBNull(reader.GetOrdinal("columns")) ? "" : reader.GetString(reader.GetOrdinal("columns"));
                    var filtersJson = reader.IsDBNull(reader.GetOrdinal("filters")) ? "" : reader.GetString(reader.GetOrdinal("filters"));
                    return new ApiResponse<ReportResponseDto>
                    {
                        Success = true,
                        Data = new ReportResponseDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Name = reader.GetString(reader.GetOrdinal("name")),
                            Module = reader.GetString(reader.GetOrdinal("module")),
                            Columns = string.IsNullOrWhiteSpace(colsCsv) ? new List<string>() : colsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                            Filters = string.IsNullOrWhiteSpace(filtersJson)
                                ? new Dictionary<string, string>()
                                : (System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(filtersJson) ?? new Dictionary<string, string>()),
                            GroupBy = reader.IsDBNull(reader.GetOrdinal("group_by")) ? null : reader.GetString(reader.GetOrdinal("group_by")),
                            SortBy = reader.IsDBNull(reader.GetOrdinal("sort_by")) ? null : reader.GetString(reader.GetOrdinal("sort_by")),
                            Query = reader.IsDBNull(reader.GetOrdinal("query")) ? null : reader.GetString(reader.GetOrdinal("query")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? AuditUserIds.System : reader.GetInt64(reader.GetOrdinal("created_by")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                            ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                            ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            LastRun = reader.GetDateTime(reader.GetOrdinal("last_run")),
                        }
                    };
                }
            }
        }

        public async Task<ApiResponse<ReportResponseDto>> Create(CreateReportDto dto)
        {
            var now = DateTime.UtcNow;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();

                var columnsCsv = string.Join(",", dto.Columns ?? new List<string>());
                var filtersJson = System.Text.Json.JsonSerializer.Serialize(dto.Filters ?? new Dictionary<string, string>(), (System.Text.Json.JsonSerializerOptions?)null);
                var createdBy = dto.CreatedBy > 0 ? dto.CreatedBy : AuditUserIds.System;

                string sql = @"
INSERT INTO reports (
    name,
    module,
    columns,
    filters,
    group_by,
    sort_by,
    query,
    is_active,
    created_by,
    created_at,
    modified_at,
    modified_by,
    last_run
)
VALUES (
    @name,
    @module,
    @columns,
    @filters,
    @group_by,
    @sort_by,
    @query,
    @is_active,
    @created_by,
    @created_at,
    @modified_at,
    @modified_by,
    @last_run
)
RETURNING id;";

                var cmd = db.GetCommand(sql);
                db.AddParameter(cmd, "name", DbTypes.Types.String).Value = dto.Name.Trim();
                db.AddParameter(cmd, "module", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Module) ? "General" : dto.Module.Trim();
                db.AddParameter(cmd, "columns", DbTypes.Types.String).Value = columnsCsv;
                db.AddParameter(cmd, "filters", DbTypes.Types.Json).Value = filtersJson;
                db.AddParameter(cmd, "group_by", DbTypes.Types.String).Value = dto.GroupBy ?? (object)DBNull.Value;
                db.AddParameter(cmd, "sort_by", DbTypes.Types.String).Value = dto.SortBy ?? (object)DBNull.Value;
                db.AddParameter(cmd, "query", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Query) ? (object)DBNull.Value : dto.Query.Trim();
                db.AddParameter(cmd, "is_active", DbTypes.Types.Boolean).Value = dto.IsActive;
                db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = createdBy;
                db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = DBNull.Value;
                db.AddParameter(cmd, "last_run", DbTypes.Types.DateTime).Value = now;

                int newId = 0;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (await reader.ReadAsync())
                        newId = reader.GetInt32(reader.GetOrdinal("id"));
                }

                var created = await GetById(newId);
                return created.Success && created.Data != null
                    ? new ApiResponse<ReportResponseDto> { Success = true, Data = created.Data }
                    : new ApiResponse<ReportResponseDto> { Success = false, Message = "Create failed" };
            }
        }

        public async Task<ApiResponse<ReportResponseDto>> Update(int id, UpdateReportDto dto)
        {
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                var now = DateTime.UtcNow;
                var columnsCsv = string.Join(",", dto.Columns ?? new List<string>());
                var filtersJson = System.Text.Json.JsonSerializer.Serialize(dto.Filters ?? new Dictionary<string, string>(), (System.Text.Json.JsonSerializerOptions?)null);

                string sql = @"
UPDATE reports
SET
    name = @name,
    module = @module,
    columns = @columns,
    filters = @filters,
    group_by = @group_by,
    sort_by = @sort_by,
    query = @query,
    is_active = @is_active,
    modified_at = @modified_at,
    modified_by = @modified_by
WHERE id = @id
RETURNING id;";

                var cmd = db.GetCommand(sql);
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                db.AddParameter(cmd, "name", DbTypes.Types.String).Value = dto.Name.Trim();
                db.AddParameter(cmd, "module", DbTypes.Types.String).Value = dto.Module.Trim();
                db.AddParameter(cmd, "columns", DbTypes.Types.String).Value = columnsCsv;
                db.AddParameter(cmd, "filters", DbTypes.Types.Json).Value = filtersJson;
                db.AddParameter(cmd, "group_by", DbTypes.Types.String).Value = dto.GroupBy ?? (object)DBNull.Value;
                db.AddParameter(cmd, "sort_by", DbTypes.Types.String).Value = dto.SortBy ?? (object)DBNull.Value;
                db.AddParameter(cmd, "query", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Query) ? (object)DBNull.Value : dto.Query.Trim();
                db.AddParameter(cmd, "is_active", DbTypes.Types.Boolean).Value = dto.IsActive ?? true;
                db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = dto.ModifiedBy ?? (object)DBNull.Value;

                int outId = 0;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (!await reader.ReadAsync())
                        return new ApiResponse<ReportResponseDto> { Success = false, Message = "Report not found" };
                    outId = reader.GetInt32(reader.GetOrdinal("id"));
                }

                var updated = await GetById(outId);
                return updated.Success && updated.Data != null
                    ? new ApiResponse<ReportResponseDto> { Success = true, Data = updated.Data }
                    : new ApiResponse<ReportResponseDto> { Success = false, Message = "Update failed" };
            }
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                string sql = @"
UPDATE reports
SET is_active=false
WHERE id=@id
RETURNING id;";
                var cmd = db.GetCommand(sql);
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (!await reader.ReadAsync())
                        return new ApiResponse<bool> { Success = false, Message = "Report not found" };
                }
                return new ApiResponse<bool> { Success = true, Data = true };
            }
        }

        public async Task<ApiResponse<ReportRunResultDto>> Run(int id, RunReportRequestDto dto)
        {
            string? reportQuery = null;
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                string selectSql = @"SELECT query FROM reports WHERE id=@id AND is_active=true LIMIT 1;";
                var cmd = db.GetCommand(selectSql);
                db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                using (DbDataReader reader = await db.Execute(cmd))
                {
                    if (await reader.ReadAsync() && !reader.IsDBNull(reader.GetOrdinal("query")))
                        reportQuery = reader.GetString(reader.GetOrdinal("query"));
                }
            }

            if (string.IsNullOrEmpty(reportQuery))
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "Report not found" };
            if (string.IsNullOrWhiteSpace(reportQuery))
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "Report has no SQL query" };

            if (!DateOnly.TryParse(dto.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDay) ||
                !DateOnly.TryParse(dto.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDay))
            {
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "Invalid StartDate or EndDate (use yyyy-MM-dd)" };
            }

            if (endDay < startDay)
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "EndDate must be on or after StartDate" };

            var startUtc = DateTime.SpecifyKind(startDay.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(endDay.ToDateTime(new TimeOnly(23, 59, 59, 999)), DateTimeKind.Utc);

            var queryText = NormalizeSqlPlaceholders(reportQuery.Trim());
            if (!IsSafeSelectSql(queryText))
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "Only single SELECT queries are allowed" };

            queryText = ApplyOrderByCreatedAt(queryText, dto.OrderCreatedAt);

            List<string> columns;
            List<Dictionary<string, object?>> rows;
            try
            {
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(queryText, conn);
                cmd.Parameters.AddWithValue("start_date", NpgsqlTypes.NpgsqlDbType.TimestampTz, startUtc);
                cmd.Parameters.AddWithValue("end_date", NpgsqlTypes.NpgsqlDbType.TimestampTz, endUtc);

                await using var reader = await cmd.ExecuteReaderAsync();
                columns = new List<string>();
                for (var i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));

                rows = new List<Dictionary<string, object?>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        row[name] = ReadCell(reader, i);
                    }
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = $"Query failed: {ex.Message}" };
            }

            // Update last_run (best-effort)
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string upd = @"UPDATE reports SET last_run=@last_run WHERE id=@id;";
                    var updCmd = db.GetCommand(upd);
                    db.AddParameter(updCmd, "last_run", DbTypes.Types.DateTime).Value = DateTime.UtcNow;
                    db.AddParameter(updCmd, "id", DbTypes.Types.Integer).Value = id;
                    await db.ExecuteNonQuery(updCmd);
                }
            }
            catch { }

            return new ApiResponse<ReportRunResultDto>
            {
                Success = true,
                Data = new ReportRunResultDto { Columns = columns, Rows = rows },
            };
        }

        private static string NormalizeSqlPlaceholders(string sql)
        {
            // Legacy :name → Npgsql @name
            sql = Regex.Replace(sql, @":start_date\b", "@start_date", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @":end_date\b", "@end_date", RegexOptions.IgnoreCase);
            return sql;
        }

        private static bool IsSafeSelectSql(string sql)
        {
            var t = sql.Trim().TrimEnd(';').Trim();
            if (!t.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return false;
            if (t.Contains(';'))
                return false;
            if (Regex.IsMatch(t, @"(?is)\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|GRANT|REVOKE|COPY)\b"))
                return false;
            if (Regex.IsMatch(t, @"(?is)\bINTO\s+(TEMP|TEMPORARY|TABLE)\b"))
                return false;
            return true;
        }

        /// <summary>When the query has a <c>FROM customers c</c> (or <c>AS c</c>) clause — not merely <c>JOIN customers c</c> — strip trailing ORDER BY and append ORDER BY c.id.</summary>
        private static string ApplyOrderByCreatedAt(string sql, string? orderCreatedAt)
        {
            if (string.IsNullOrWhiteSpace(orderCreatedAt))
                return sql;
            var trimmed = sql.Trim().TrimEnd(';').Trim();
            // Require "FROM customers c", not "JOIN customers c" (invoices), and not reports with no customers table.
            if (!Regex.IsMatch(trimmed, @"(?is)^SELECT\b[\s\S]*?\bFROM\s+customers\s+(AS\s+)?c\b"))
                return sql;
            var dir = orderCreatedAt.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            var withoutOrder = Regex.Replace(trimmed, @"\s+ORDER\s+BY\s+.+$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return $"{withoutOrder} ORDER BY c.id {dir}";
        }

        private static object? ReadCell(NpgsqlDataReader reader, int i)
        {
            if (reader.IsDBNull(i))
                return null;
            var v = reader.GetValue(i);
            return v switch
            {
                DateTime dt => dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt,
                _ => v,
            };
        }
    }
}
