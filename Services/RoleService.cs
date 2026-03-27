using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public interface IRoleService
    {
        Task<ApiResponse<List<RoleResponseDto>>> GetAll();
        Task<ApiResponse<RoleResponseDto>> GetById(int id);
        Task<ApiResponse<RoleResponseDto>> Create(CreateRoleDto dto);
        Task<ApiResponse<RoleResponseDto>> Update(int id, UpdateRoleDto dto);
        Task<ApiResponse<bool>> Delete(int id);
    }

    public class RoleService : IRoleService
    {
        private readonly CrmDbContext _context;

        public RoleService(CrmDbContext context)
        {
            _context = context;
        }

        private static RoleResponseDto Map(Role r, int userCount) => new()
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Permissions = r.Permissions ?? new List<string>(),
            UserCount = userCount,
            CreatedAt = r.CreatedAt,
            CreatedBy = r.CreatedBy,
            ModifiedAt = r.ModifiedAt,
            ModifiedBy = r.ModifiedBy
        };

        private async Task<Dictionary<string, int>> UserCountsByRoleNameAsync()
        {
            return await _context.Users.AsNoTracking()
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Cnt = g.Count() })
                .ToDictionaryAsync(x => x.Role, x => x.Cnt);
        }

        public async Task<ApiResponse<List<RoleResponseDto>>> GetAll()
        {
            try
            {
                var roles = await _context.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
                var counts = await UserCountsByRoleNameAsync();
                var data = roles.Select(r => Map(r, counts.TryGetValue(r.Name, out var c) ? c : 0)).ToList();
                return new ApiResponse<List<RoleResponseDto>> { Success = true, Data = data };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<RoleResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RoleResponseDto>> GetById(int id)
        {
            try
            {
                var r = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (r == null) return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role not found" };
                var counts = await UserCountsByRoleNameAsync();
                var n = counts.TryGetValue(r.Name, out var c) ? c : 0;
                return new ApiResponse<RoleResponseDto> { Success = true, Data = Map(r, n) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RoleResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RoleResponseDto>> Create(CreateRoleDto dto)
        {
            try
            {
                var name = dto.Name.Trim();
                if (string.IsNullOrEmpty(name))
                    return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role name is required" };

                var now = DateTime.UtcNow;
                var r = new Role
                {
                    Name = name,
                    Description = dto.Description?.Trim() ?? string.Empty,
                    Permissions = dto.Permissions ?? new List<string>(),
                    UserCount = 0,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Roles.Add(r);
                await _context.SaveChangesAsync();
                return new ApiResponse<RoleResponseDto> { Success = true, Data = Map(r, 0) };
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<RoleResponseDto>
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RoleResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<RoleResponseDto>> Update(int id, UpdateRoleDto dto)
        {
            try
            {
                var r = await _context.Roles.FirstOrDefaultAsync(x => x.Id == id);
                if (r == null) return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role not found" };

                var newName = dto.Name.Trim();
                if (string.IsNullOrEmpty(newName))
                    return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role name is required" };

                var oldName = r.Name;
                if (!string.Equals(oldName, newName, StringComparison.Ordinal))
                {
                    var taken = await _context.Roles.AnyAsync(x => x.Name == newName && x.Id != id);
                    if (taken)
                        return new ApiResponse<RoleResponseDto> { Success = false, Message = "A role with this name already exists" };

                    const string by = "System";
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE users SET role = {newName}, modified_at = {DateTime.UtcNow}, modified_by = {by} WHERE role = {oldName}");
                }

                r.Name = newName;
                r.Description = dto.Description?.Trim() ?? string.Empty;
                r.Permissions = dto.Permissions ?? new List<string>();
                r.ModifiedAt = DateTime.UtcNow;
                r.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();

                var counts = await UserCountsByRoleNameAsync();
                var n = counts.TryGetValue(r.Name, out var c) ? c : 0;
                return new ApiResponse<RoleResponseDto> { Success = true, Data = Map(r, n) };
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<RoleResponseDto>
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<RoleResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> Delete(int id)
        {
            try
            {
                var r = await _context.Roles.FirstOrDefaultAsync(x => x.Id == id);
                if (r == null) return new ApiResponse<bool> { Success = false, Message = "Role not found" };

                var assigned = await _context.Users.CountAsync(u => u.Role == r.Name);
                if (assigned > 0)
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = $"Cannot delete role assigned to {assigned} user(s). Reassign users first."
                    };

                _context.Roles.Remove(r);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (DbUpdateException ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = ex.InnerException?.Message ?? ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
