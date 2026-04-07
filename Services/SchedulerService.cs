using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    public interface ISchedulerService
    {
        Task<ApiResponse<List<SchedulerEventResponseDto>>> GetAll();
        Task<ApiResponse<SchedulerEventResponseDto>> GetById(int id);
        Task<ApiResponse<SchedulerEventResponseDto>> Create(CreateSchedulerEventDto dto);
        Task<ApiResponse<SchedulerEventResponseDto>> Update(int id, UpdateSchedulerEventDto dto);
        Task<ApiResponse<bool>> Delete(int id);
    }

    public class SchedulerService : ISchedulerService
    {
        IDbProvider dbprovider;

        public SchedulerService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        private static bool IsActiveForStatus(string status) =>
            !string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase);

        private static SchedulerEventResponseDto Map(SchedulerEvent e) => new()
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Attendees = e.Attendees ?? new List<int>(),
            Location = e.Location,
            Type = e.Type,
            Priority = e.Priority,
            Status = string.IsNullOrWhiteSpace(e.Status) ? "scheduled" : e.Status,
            IsActive = e.IsActive,
            RelatedToType = e.RelatedToType,
            RelatedToId = e.RelatedToId,
            CreatedBy = e.CreatedBy,
            CreatedAt = e.CreatedAt,
            ModifiedAt = e.ModifiedAt,
            ModifiedBy = e.ModifiedBy
        };

        public async Task<ApiResponse<List<SchedulerEventResponseDto>>> GetAll()
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    title,
    description,
    start_time,
    end_time,
    attendees,
    location,
    type,
    priority,
    status,
    is_active,
    related_to_type,
    related_to_id,
    created_by,
    created_at,
    modified_at,
    modified_by
FROM scheduler_events
WHERE is_active = true
ORDER BY id DESC;";
                    var cmd = db.GetCommand(sql);
                    var list = new List<SchedulerEventResponseDto>();
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                        {
                            var attendeesCsv = reader.GetString(reader.GetOrdinal("attendees"));
                            var attendees = string.IsNullOrWhiteSpace(attendeesCsv)
                                ? new List<int>()
                                : attendeesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

                            list.Add(new SchedulerEventResponseDto
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.GetString(reader.GetOrdinal("description")),
                                StartTime = reader.GetDateTime(reader.GetOrdinal("start_time")),
                                EndTime = reader.GetDateTime(reader.GetOrdinal("end_time")),
                                Attendees = attendees,
                                Location = reader.IsDBNull(reader.GetOrdinal("location")) ? null : reader.GetString(reader.GetOrdinal("location")),
                                Type = reader.GetString(reader.GetOrdinal("type")),
                                Priority = reader.GetString(reader.GetOrdinal("priority")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                RelatedToType = reader.IsDBNull(reader.GetOrdinal("related_to_type")) ? null : reader.GetString(reader.GetOrdinal("related_to_type")),
                                RelatedToId = reader.IsDBNull(reader.GetOrdinal("related_to_id")) ? null : reader.GetInt32(reader.GetOrdinal("related_to_id")),
                                CreatedBy = reader.GetInt64(reader.GetOrdinal("created_by")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            });
                        }
                    }
                    return new ApiResponse<List<SchedulerEventResponseDto>> { Success = true, Data = list };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<SchedulerEventResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<SchedulerEventResponseDto>> GetById(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    title,
    description,
    start_time,
    end_time,
    attendees,
    location,
    type,
    priority,
    status,
    is_active,
    related_to_type,
    related_to_id,
    created_by,
    created_at,
    modified_at,
    modified_by
FROM scheduler_events
WHERE id = @id AND is_active = true
LIMIT 1;";
                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = "Event not found" };

                        var attendeesCsv = reader.GetString(reader.GetOrdinal("attendees"));
                        var attendees = string.IsNullOrWhiteSpace(attendeesCsv)
                            ? new List<int>()
                            : attendeesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

                        return new ApiResponse<SchedulerEventResponseDto>
                        {
                            Success = true,
                            Data = new SchedulerEventResponseDto
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.GetString(reader.GetOrdinal("description")),
                                StartTime = reader.GetDateTime(reader.GetOrdinal("start_time")),
                                EndTime = reader.GetDateTime(reader.GetOrdinal("end_time")),
                                Attendees = attendees,
                                Location = reader.IsDBNull(reader.GetOrdinal("location")) ? null : reader.GetString(reader.GetOrdinal("location")),
                                Type = reader.GetString(reader.GetOrdinal("type")),
                                Priority = reader.GetString(reader.GetOrdinal("priority")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                RelatedToType = reader.IsDBNull(reader.GetOrdinal("related_to_type")) ? null : reader.GetString(reader.GetOrdinal("related_to_type")),
                                RelatedToId = reader.IsDBNull(reader.GetOrdinal("related_to_id")) ? null : reader.GetInt32(reader.GetOrdinal("related_to_id")),
                                CreatedBy = reader.GetInt64(reader.GetOrdinal("created_by")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<SchedulerEventResponseDto>> Create(CreateSchedulerEventDto dto)
        {
            try
            {
                var now = DateTime.UtcNow;
                var status = string.IsNullOrWhiteSpace(dto.Status) ? "scheduled" : dto.Status.Trim();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var attendeesCsv = string.Join(",", dto.Attendees ?? new List<int>());

                    string sql = @"
INSERT INTO scheduler_events (
    title,
    description,
    start_time,
    end_time,
    attendees,
    location,
    type,
    priority,
    status,
    is_active,
    related_to_type,
    related_to_id,
    created_by,
    created_at,
    modified_at,
    modified_by
)
VALUES (
    @title,
    @description,
    @start_time,
    @end_time,
    @attendees,
    @location,
    @type,
    @priority,
    @status,
    @is_active,
    @related_to_type,
    @related_to_id,
    @created_by,
    @created_at,
    @modified_at,
    @modified_by
)
RETURNING id;";
                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "title", DbTypes.Types.String).Value = dto.Title.Trim();
                    db.AddParameter(cmd, "description", DbTypes.Types.String).Value = dto.Description?.Trim() ?? string.Empty;
                    db.AddParameter(cmd, "start_time", DbTypes.Types.DateTime).Value = dto.StartTime;
                    db.AddParameter(cmd, "end_time", DbTypes.Types.DateTime).Value = dto.EndTime;
                    db.AddParameter(cmd, "attendees", DbTypes.Types.String).Value = attendeesCsv;
                    db.AddParameter(cmd, "location", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Location) ? (object)DBNull.Value : dto.Location.Trim();
                    db.AddParameter(cmd, "type", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Type) ? "meeting" : dto.Type.Trim();
                    db.AddParameter(cmd, "priority", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Priority) ? "medium" : dto.Priority.Trim();
                    db.AddParameter(cmd, "status", DbTypes.Types.String).Value = status;
                    db.AddParameter(cmd, "is_active", DbTypes.Types.Boolean).Value = IsActiveForStatus(status);
                    db.AddParameter(cmd, "related_to_type", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.RelatedToType) ? (object)DBNull.Value : dto.RelatedToType.Trim();
                    db.AddParameter(cmd, "related_to_id", DbTypes.Types.Integer).Value = dto.RelatedToId.HasValue ? dto.RelatedToId.Value : DBNull.Value;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(cmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    int newId = 0;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            newId = reader.GetInt32(reader.GetOrdinal("id"));
                    }
                    return await GetById(newId);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<SchedulerEventResponseDto>> Update(int id, UpdateSchedulerEventDto dto)
        {
            try
            {
                var status = string.IsNullOrWhiteSpace(dto.Status) ? "scheduled" : dto.Status.Trim();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    var now = DateTime.UtcNow;
                    var attendeesCsv = string.Join(",", dto.Attendees ?? new List<int>());

                    string sql = @"
UPDATE scheduler_events
SET
    title = @title,
    description = @description,
    start_time = @start_time,
    end_time = @end_time,
    attendees = @attendees,
    location = @location,
    type = @type,
    priority = @priority,
    status = @status,
    is_active = @is_active,
    related_to_type = @related_to_type,
    related_to_id = @related_to_id,
    modified_at = @modified_at,
    modified_by = @modified_by
WHERE id = @id
RETURNING id;";

                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(cmd, "title", DbTypes.Types.String).Value = dto.Title.Trim();
                    db.AddParameter(cmd, "description", DbTypes.Types.String).Value = dto.Description?.Trim() ?? string.Empty;
                    db.AddParameter(cmd, "start_time", DbTypes.Types.DateTime).Value = dto.StartTime;
                    db.AddParameter(cmd, "end_time", DbTypes.Types.DateTime).Value = dto.EndTime;
                    db.AddParameter(cmd, "attendees", DbTypes.Types.String).Value = attendeesCsv;
                    db.AddParameter(cmd, "location", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Location) ? (object)DBNull.Value : dto.Location.Trim();
                    db.AddParameter(cmd, "type", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Type) ? "meeting" : dto.Type.Trim();
                    db.AddParameter(cmd, "priority", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.Priority) ? "medium" : dto.Priority.Trim();
                    db.AddParameter(cmd, "status", DbTypes.Types.String).Value = status;
                    db.AddParameter(cmd, "is_active", DbTypes.Types.Boolean).Value = IsActiveForStatus(status);
                    db.AddParameter(cmd, "related_to_type", DbTypes.Types.String).Value = string.IsNullOrWhiteSpace(dto.RelatedToType) ? (object)DBNull.Value : dto.RelatedToType.Trim();
                    db.AddParameter(cmd, "related_to_id", DbTypes.Types.Integer).Value = dto.RelatedToId.HasValue ? dto.RelatedToId.Value : DBNull.Value;
                    db.AddParameter(cmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = "Event not found" };
                    }
                    return await GetById(id);
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<SchedulerEventResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
UPDATE scheduler_events
SET is_active=false
WHERE id=@id
RETURNING id;";
                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<bool> { Success = false, Message = "Event not found" };
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
