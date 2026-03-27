using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

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
        private readonly CrmDbContext _context;

        public FileService(CrmDbContext context)
        {
            _context = context;
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
                var row = new CrmFile
                {
                    Content = content,
                    Type = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType.Trim(),
                    Version = 1,
                    CreatedBy = createdBy,
                    CreatedOn = now,
                    IsActive = true,
                    IsSuspended = false,
                    IsFactory = false,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    ParentId = parentId
                };
                _context.Files.Add(row);
                await _context.SaveChangesAsync();
                return new ApiResponse<FileStoredResponseDto>
                {
                    Success = true,
                    Data = new FileStoredResponseDto { ImageId = row.Id, Type = row.Type }
                };
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
                var row = await _context.Files.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == id && f.IsActive && !f.IsSuspended);
                if (row == null)
                    return new ApiResponse<(byte[] Content, string ContentType)> { Success = false, Message = "File not found" };
                var ct = string.IsNullOrWhiteSpace(row.Type) ? "application/octet-stream" : row.Type;
                return new ApiResponse<(byte[] Content, string ContentType)>
                {
                    Success = true,
                    Data = (row.Content, ct)
                };
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
                var row = await _context.Files.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == id);
                if (row == null)
                    return new ApiResponse<FileMetadataResponseDto> { Success = false, Message = "File not found" };
                return new ApiResponse<FileMetadataResponseDto>
                {
                    Success = true,
                    Data = new FileMetadataResponseDto
                    {
                        Id = row.Id,
                        Type = row.Type,
                        Version = row.Version,
                        CreatedOn = row.CreatedOn,
                        CreatedBy = row.CreatedBy,
                        IsActive = row.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<FileMetadataResponseDto> { Success = false, Message = ex.Message };
            }
        }
    }
}
