using System.Security.Cryptography;
using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>UserService.ts</c>.</summary>
    public interface IUserService
    {
        Task<ApiResponse<LoginResponseDto>> Login(LoginRequestDto dto);
        Task<ApiResponse<List<UserResponseDto>>> GetAll();
        Task<ApiResponse<UserResponseDto>> GetById(int id);
        Task<ApiResponse<UserResponseDto>> GetByEmail(string email);
        Task<ApiResponse<List<UserResponseDto>>> GetByStatus(string status);
        Task<ApiResponse<UserResponseDto>> CreateUser(CreateUserDto dto);
        Task<ApiResponse<UserResponseDto>> UpdateUser(int id, UpdateUserDto dto);
        Task<ApiResponse<bool>> DeleteUser(int id);
    }

    public class UserService : IUserService
    {
        private readonly CrmDbContext _context;

        public UserService(CrmDbContext context)
        {
            _context = context;
        }

        private static UserResponseDto Map(User u, List<string> permissions) => new()
        {
            Id = u.Id,
            UserId = u.UserLoginId,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Role = u.Role,
            Permissions = permissions,
            IsActive = u.IsActive,
            LastLogin = u.LastLogin,
            CreatedAt = u.CreatedAt,
            CreatedBy = u.CreatedBy,
            ModifiedAt = u.ModifiedAt,
            ModifiedBy = u.ModifiedBy
        };

        private async Task<Dictionary<string, List<string>>> LoadRolePermissionsMapAsync()
        {
            var roles = await _context.Roles.AsNoTracking().ToListAsync();
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in roles)
            {
                if (string.IsNullOrWhiteSpace(r.Name)) continue;
                map[r.Name.Trim()] = r.Permissions?.ToList() ?? new List<string>();
            }

            return map;
        }

        private static List<string> PermissionsForRole(User u, Dictionary<string, List<string>> map)
        {
            if (string.IsNullOrWhiteSpace(u.Role)) return new List<string>();
            return map.TryGetValue(u.Role.Trim(), out var p) ? p : new List<string>();
        }

        /// <summary>Opaque refresh token (persist server-side / issue JWT in a follow-up).</summary>
        private static string CreateRefreshToken()
        {
            var bytes = new byte[48];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static bool VerifyPassword(User user, string password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                try
                {
                    return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch
                {
                    return false;
                }
            }

            // Dev fallback when password_hash is not set (parity with core-crm-suite mock login)
            var expected = user.FirstName.Length > 0
                ? char.ToLowerInvariant(user.FirstName[0]).ToString()
                : string.Empty;
            return string.Equals(password, expected, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(password, "password", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ApiResponse<LoginResponseDto>> Login(LoginRequestDto dto)
        {
            try
            {
                var email = dto.Email?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(dto.Password))
                    return new ApiResponse<LoginResponseDto> { Success = false, Message = "Email and password are required" };

                var emailNorm = email.ToLowerInvariant();
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNorm);

                if (user == null)
                    return new ApiResponse<LoginResponseDto> { Success = false, Message = "Invalid email or password" };

                if (!user.IsActive)
                    return new ApiResponse<LoginResponseDto> { Success = false, Message = "Account is inactive" };

                if (!VerifyPassword(user, dto.Password))
                    return new ApiResponse<LoginResponseDto> { Success = false, Message = "Invalid email or password" };

                user.LastLogin = DateTime.UtcNow;
                user.ModifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var permMap = await LoadRolePermissionsMapAsync();
                var permissions = PermissionsForRole(user, permMap);

                var body = new LoginResponseDto
                {
                    UserId = user.Id,
                    Username = user.UserLoginId,
                    RefreshToken = CreateRefreshToken(),
                    Role = user.Role,
                    Permissions = permissions
                };

                return new ApiResponse<LoginResponseDto> { Success = true, Data = body };
            }
            catch (Exception ex)
            {
                return new ApiResponse<LoginResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<UserResponseDto>>> GetAll()
        {
            try
            {
                var permMap = await LoadRolePermissionsMapAsync();
                var list = await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
                return new ApiResponse<List<UserResponseDto>>
                {
                    Success = true,
                    Data = list.Select(u => Map(u, PermissionsForRole(u, permMap))).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<UserResponseDto>> GetById(int id)
        {
            try
            {
                var u = await _context.Users.FindAsync(id);
                if (u == null) return new ApiResponse<UserResponseDto> { Success = false, Message = "User not found" };
                var permMap = await LoadRolePermissionsMapAsync();
                return new ApiResponse<UserResponseDto> { Success = true, Data = Map(u, PermissionsForRole(u, permMap)) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<UserResponseDto>> GetByEmail(string email)
        {
            try
            {
                var u = await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
                if (u == null) return new ApiResponse<UserResponseDto> { Success = false, Message = "User not found" };
                var permMap = await LoadRolePermissionsMapAsync();
                return new ApiResponse<UserResponseDto> { Success = true, Data = Map(u, PermissionsForRole(u, permMap)) };
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<UserResponseDto>>> GetByStatus(string status)
        {
            try
            {
                var active = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
                var permMap = await LoadRolePermissionsMapAsync();
                var list = await _context.Users.Where(u => u.IsActive == active).OrderByDescending(u => u.CreatedAt).ToListAsync();
                return new ApiResponse<List<UserResponseDto>>
                {
                    Success = true,
                    Data = list.Select(u => Map(u, PermissionsForRole(u, permMap))).ToList()
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<UserResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<UserResponseDto>> CreateUser(CreateUserDto dto)
        {
            try
            {
                var loginId = dto.UserId?.Trim() ?? string.Empty;
                var email = dto.Email?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(loginId))
                    return new ApiResponse<UserResponseDto> { Success = false, Message = "Login ID is required" };
                if (string.IsNullOrEmpty(email))
                    return new ApiResponse<UserResponseDto> { Success = false, Message = "Email is required" };
                if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                    return new ApiResponse<UserResponseDto>
                    {
                        Success = false,
                        Message = "Password is required and must be at least 6 characters"
                    };

                var emailNorm = email.ToLowerInvariant();
                if (await _context.Users.AnyAsync(u => u.UserLoginId.ToLower() == loginId.ToLowerInvariant()))
                    return new ApiResponse<UserResponseDto> { Success = false, Message = "Login ID is already in use" };
                if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailNorm))
                    return new ApiResponse<UserResponseDto> { Success = false, Message = "Email is already in use" };

                var now = DateTime.UtcNow;
                var u = new User
                {
                    UserLoginId = loginId,
                    FirstName = dto.FirstName?.Trim() ?? string.Empty,
                    LastName = dto.LastName?.Trim() ?? string.Empty,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Role = dto.Role?.Trim() ?? string.Empty,
                    IsActive = dto.IsActive,
                    LastLogin = now,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                _context.Users.Add(u);
                await _context.SaveChangesAsync();
                var permMap = await LoadRolePermissionsMapAsync();
                return new ApiResponse<UserResponseDto> { Success = true, Data = Map(u, PermissionsForRole(u, permMap)) };
            }
            catch (DbUpdateException dbEx)
            {
                var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                return new ApiResponse<UserResponseDto>
                {
                    Success = false,
                    Message = $"Could not save user: {inner}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<UserResponseDto>> UpdateUser(int id, UpdateUserDto dto)
        {
            try
            {
                var u = await _context.Users.FindAsync(id);
                if (u == null) return new ApiResponse<UserResponseDto> { Success = false, Message = "User not found" };
                if (!string.IsNullOrWhiteSpace(dto.FirstName)) u.FirstName = dto.FirstName;
                if (!string.IsNullOrWhiteSpace(dto.LastName)) u.LastName = dto.LastName;
                if (!string.IsNullOrWhiteSpace(dto.Email)) u.Email = dto.Email;
                if (!string.IsNullOrWhiteSpace(dto.Role)) u.Role = dto.Role;
                if (dto.IsActive.HasValue) u.IsActive = dto.IsActive.Value;
                if (!string.IsNullOrWhiteSpace(dto.Password))
                {
                    if (dto.Password.Length < 6)
                        return new ApiResponse<UserResponseDto>
                        {
                            Success = false,
                            Message = "Password must be at least 6 characters"
                        };
                    u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                }

                u.ModifiedAt = DateTime.UtcNow;
                u.ModifiedBy = AuditUserIds.System;
                await _context.SaveChangesAsync();
                var permMap = await LoadRolePermissionsMapAsync();
                return new ApiResponse<UserResponseDto> { Success = true, Data = Map(u, PermissionsForRole(u, permMap)) };
            }
            catch (DbUpdateException dbEx)
            {
                var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                return new ApiResponse<UserResponseDto>
                {
                    Success = false,
                    Message = $"Could not save user: {inner}"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<UserResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteUser(int id)
        {
            try
            {
                var u = await _context.Users.FindAsync(id);
                if (u == null) return new ApiResponse<bool> { Success = false, Message = "User not found" };
                _context.Users.Remove(u);
                await _context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }
    }
}
