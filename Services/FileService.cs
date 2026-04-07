using CRM.Server.DTOs;
using CRM.Server.Utils;
using System.Data.Common;

namespace CRM.Server.Services
{
    public interface IFileService
    {
        Task<ApiResponse<FileStoredResponseDto>> StoreAsync(
            byte[] content,
            string? mimeType,
            long? createdBy,
            string? notes,
            long? parentId);

        Task<ApiResponse<(byte[] Content, string ContentType)>> GetContentAsync(long id);

        Task<ApiResponse<FileMetadataResponseDto>> GetMetadataAsync(long id);
    }

    public class FileService : IFileService
    {
        IDbProvider dbprovider;

        public FileService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
        }

        public async Task<ApiResponse<FileStoredResponseDto>> StoreAsync(
            byte[] content,
            string? mimeType,
            long? createdBy,
            string? notes,
            long? parentId)
        {
            try
            {
                if (content == null || content.Length == 0)
                    return new ApiResponse<FileStoredResponseDto> { Success = false, Message = "File content is empty" };

                var now = DateTime.UtcNow;
                var type = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType.Trim();
                var cleanNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string sql = @"
INSERT INTO files (
    is_factory,
    content,
    version,
    created_by,
    created_on,
    modified_by,
    modified_on,
    attributes,
    is_active,
    is_suspended,
    parent_id,
    notes,
    type
)
VALUES (
    false,
    @content,
    1,
    @created_by,
    @created_on,
    @modified_by,
    @modified_on,
    @attributes,
    true,
    false,
    @parent_id,
    @notes,
    @type
)
RETURNING id;";

                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "content", DbTypes.Types.ByteArray).Value = content;
                    db.AddParameter(cmd, "created_by", DbTypes.Types.Long).Value = createdBy.HasValue ? createdBy.Value : DBNull.Value;
                    db.AddParameter(cmd, "created_on", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "modified_by", DbTypes.Types.Long).Value = createdBy.HasValue ? createdBy.Value : DBNull.Value;
                    db.AddParameter(cmd, "modified_on", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(cmd, "attributes", DbTypes.Types.Json).Value = DBNull.Value;
                    db.AddParameter(cmd, "parent_id", DbTypes.Types.Long).Value = parentId.HasValue ? parentId.Value : DBNull.Value;
                    db.AddParameter(cmd, "notes", DbTypes.Types.String).Value = cleanNotes ?? (object)DBNull.Value;
                    db.AddParameter(cmd, "type", DbTypes.Types.String).Value = type;

                    long id = 0;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                            id = reader.GetInt64(reader.GetOrdinal("id"));
                    }

                    return new ApiResponse<FileStoredResponseDto>
                    {
                        Success = true,
                        Data = new FileStoredResponseDto { ImageId = id, Type = type }
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<FileStoredResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<(byte[] Content, string ContentType)>> GetContentAsync(long id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT content, type
FROM files
WHERE id = @id AND is_active = true AND is_suspended = false
LIMIT 1;";
                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "id", DbTypes.Types.Long).Value = id;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<(byte[] Content, string ContentType)> { Success = false, Message = "File not found" };

                        var content = (byte[])reader["content"];
                        var ct = reader.IsDBNull(reader.GetOrdinal("type"))
                            ? "application/octet-stream"
                            : reader.GetString(reader.GetOrdinal("type"));

                        if (string.IsNullOrWhiteSpace(ct)) ct = "application/octet-stream";

                        return new ApiResponse<(byte[] Content, string ContentType)>
                        {
                            Success = true,
                            Data = (content, ct)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<(byte[] Content, string ContentType)> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<FileMetadataResponseDto>> GetMetadataAsync(long id)
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT id, type, version, created_on, created_by, is_active
FROM files
WHERE id = @id
LIMIT 1;";
                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "id", DbTypes.Types.Long).Value = id;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<FileMetadataResponseDto> { Success = false, Message = "File not found" };

                        return new ApiResponse<FileMetadataResponseDto>
                        {
                            Success = true,
                            Data = new FileMetadataResponseDto
                            {
                                Id = reader.GetInt64(reader.GetOrdinal("id")),
                                Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                Version = reader.GetInt32(reader.GetOrdinal("version")),
                                CreatedOn = reader.GetDateTime(reader.GetOrdinal("created_on")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                            }
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<FileMetadataResponseDto> { Success = false, Message = ex.Message };
            }
        }
    }
}

