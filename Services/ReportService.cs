using System.Globalization;
using System.Text.RegularExpressions;
using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;
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
        private readonly CrmDbContext _context;
        private readonly NpgsqlDataSource _dataSource;

        public ReportService(CrmDbContext context, NpgsqlDataSource dataSource)
        {
            _context = context;
            _dataSource = dataSource;
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
            var rows = await _context.Reports.AsNoTracking()
                .OrderByDescending(r => r.ModifiedAt)
                .ToListAsync();
            return new ApiResponse<List<ReportResponseDto>>
            {
                Success = true,
                Data = rows.Select(MapReport).ToList(),
            };
        }

        public async Task<ApiResponse<ReportResponseDto>> GetById(int id)
        {
            var r = await _context.Reports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (r == null)
                return new ApiResponse<ReportResponseDto> { Success = false, Message = "Report not found" };
            return new ApiResponse<ReportResponseDto> { Success = true, Data = MapReport(r) };
        }

        public async Task<ApiResponse<ReportResponseDto>> Create(CreateReportDto dto)
        {
            var now = DateTime.UtcNow;
            var entity = new Report
            {
                Name = dto.Name.Trim(),
                Module = string.IsNullOrWhiteSpace(dto.Module) ? "General" : dto.Module.Trim(),
                Columns = dto.Columns ?? new List<string>(),
                Filters = dto.Filters ?? new Dictionary<string, string>(),
                GroupBy = dto.GroupBy,
                SortBy = dto.SortBy,
                Query = string.IsNullOrWhiteSpace(dto.Query) ? null : dto.Query.Trim(),
                IsActive = dto.IsActive,
                CreatedBy = dto.CreatedBy > 0 ? dto.CreatedBy : AuditUserIds.System,
                CreatedAt = now,
                ModifiedAt = now,
                LastRun = now,
            };
            _context.Reports.Add(entity);
            await _context.SaveChangesAsync();
            return new ApiResponse<ReportResponseDto> { Success = true, Data = MapReport(entity) };
        }

        public async Task<ApiResponse<ReportResponseDto>> Update(int id, UpdateReportDto dto)
        {
            var entity = await _context.Reports.FindAsync(id);
            if (entity == null)
                return new ApiResponse<ReportResponseDto> { Success = false, Message = "Report not found" };

            entity.Name = dto.Name.Trim();
            entity.Module = dto.Module.Trim();
            entity.Columns = dto.Columns ?? new List<string>();
            entity.Filters = dto.Filters ?? new Dictionary<string, string>();
            entity.GroupBy = dto.GroupBy;
            entity.SortBy = dto.SortBy;
            entity.Query = string.IsNullOrWhiteSpace(dto.Query) ? null : dto.Query.Trim();
            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;
            entity.ModifiedAt = DateTime.UtcNow;
            entity.ModifiedBy = dto.ModifiedBy;
            await _context.SaveChangesAsync();
            return new ApiResponse<ReportResponseDto> { Success = true, Data = MapReport(entity) };
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            var entity = await _context.Reports.FindAsync(id);
            if (entity == null)
                return new ApiResponse<bool> { Success = false, Message = "Report not found" };
            _context.Reports.Remove(entity);
            await _context.SaveChangesAsync();
            return new ApiResponse<bool> { Success = true, Data = true };
        }

        public async Task<ApiResponse<ReportRunResultDto>> Run(int id, RunReportRequestDto dto)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report == null)
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "Report not found" };
            if (string.IsNullOrWhiteSpace(report.Query))
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

            var sql = NormalizeSqlPlaceholders(report.Query.Trim());
            if (!IsSafeSelectSql(sql))
                return new ApiResponse<ReportRunResultDto> { Success = false, Message = "Only single SELECT queries are allowed" };

            sql = ApplyOrderByCreatedAt(sql, dto.OrderCreatedAt);

            List<string> columns;
            List<Dictionary<string, object?>> rows;
            try
            {
                await using var conn = await _dataSource.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand(sql, conn);
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

            report.LastRun = DateTime.UtcNow;
            await _context.SaveChangesAsync();

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

        /// <summary>When the query has a <c>FROM customers c</c> (or <c>AS c</c>) clause — not merely <c>JOIN customers c</c> — strip trailing ORDER BY and append ORDER BY c.created_at.</summary>
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
            return $"{withoutOrder} ORDER BY c.created_at {dir}";
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
