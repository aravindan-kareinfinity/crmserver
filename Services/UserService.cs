using System.Security.Cryptography;
using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

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
        IDbProvider dbprovider;
        IQueryBuilderProvider querybuilderprovider;

        public UserService(IDbProvider dbprovider, IQueryBuilderProvider querybuilderprovider)
        {
            this.dbprovider = dbprovider;
            this.querybuilderprovider = querybuilderprovider;
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
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                string sql = @"SELECT name, permissions FROM roles WHERE is_active = true ORDER BY id DESC;";
                var command = db.GetCommand(sql);
                using (DbDataReader reader = await db.Execute(command))
                {
                    while (await reader.ReadAsync())
                    {
                        var roleName = reader.IsDBNull(reader.GetOrdinal("name")) ? "" : reader.GetString(reader.GetOrdinal("name"));
                        if (string.IsNullOrWhiteSpace(roleName)) continue;

                        var permissionsCsv = reader.IsDBNull(reader.GetOrdinal("permissions")) ? "" : reader.GetString(reader.GetOrdinal("permissions"));
                        var perms = string.IsNullOrWhiteSpace(permissionsCsv)
                            ? new List<string>()
                            : permissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                        map[roleName.Trim()] = perms;
                    }
                }
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
                User? user = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    user_id,
    first_name,
    last_name,
    email,
    password_hash,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
FROM users
WHERE lower(email) = @email
  AND is_active = true
LIMIT 1;";

                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "email", DbTypes.Types.String).Value = emailNorm;

                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new User
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                UserLoginId = reader.GetString(reader.GetOrdinal("user_id")),
                                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                                Email = reader.GetString(reader.GetOrdinal("email")),
                                PasswordHash = reader.IsDBNull(reader.GetOrdinal("password_hash"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("password_hash")),
                                Role = reader.GetString(reader.GetOrdinal("role")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                LastLogin = reader.GetDateTime(reader.GetOrdinal("last_login")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            };
                        }
                    }

                    if (user == null)
                        return new ApiResponse<LoginResponseDto> { Success = false, Message = "Invalid email or password" };

                    if (!user.IsActive)
                        return new ApiResponse<LoginResponseDto> { Success = false, Message = "Account is inactive" };

                    if (!VerifyPassword(user, dto.Password))
                        return new ApiResponse<LoginResponseDto> { Success = false, Message = "Invalid email or password" };

                    var now = DateTime.UtcNow;
                    user.LastLogin = now;
                    user.ModifiedAt = now;

                    string updateSql = @"
UPDATE users
SET last_login = @last_login,
    modified_at = @modified_at
WHERE id = @id;";

                    var updateCmd = db.GetCommand(updateSql);
                    db.AddParameter(updateCmd, "last_login", DbTypes.Types.DateTime).Value = user.LastLogin;
                    db.AddParameter(updateCmd, "modified_at", DbTypes.Types.DateTime).Value = user.ModifiedAt;
                    db.AddParameter(updateCmd, "id", DbTypes.Types.Integer).Value = user.Id;
                    await db.ExecuteNonQuery(updateCmd);
                }

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
                List<User> list = new List<User>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    user_id,
    first_name,
    last_name,
    email,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
FROM users
WHERE is_active = true
ORDER BY id DESC;";

                    var cmd = db.GetCommand(sql);
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new User
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                UserLoginId = reader.GetString(reader.GetOrdinal("user_id")),
                                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                                Email = reader.GetString(reader.GetOrdinal("email")),
                                Role = reader.GetString(reader.GetOrdinal("role")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                LastLogin = reader.GetDateTime(reader.GetOrdinal("last_login")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            });
                        }
                    }
                }
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
                User? u = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    user_id,
    first_name,
    last_name,
    email,
    password_hash,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
FROM users
WHERE id = @id
LIMIT 1;";

                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "id", DbTypes.Types.Integer).Value = id;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                        {
                            u = new User
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                UserLoginId = reader.GetString(reader.GetOrdinal("user_id")),
                                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                                Email = reader.GetString(reader.GetOrdinal("email")),
                                PasswordHash = reader.IsDBNull(reader.GetOrdinal("password_hash"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("password_hash")),
                                Role = reader.GetString(reader.GetOrdinal("role")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                LastLogin = reader.GetDateTime(reader.GetOrdinal("last_login")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            };
                        }
                    }
                }

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
                User? u = null;
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    user_id,
    first_name,
    last_name,
    email,
    password_hash,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
FROM users
WHERE email = @email
LIMIT 1;";

                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "email", DbTypes.Types.String).Value = email;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        if (await reader.ReadAsync())
                        {
                            u = new User
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                UserLoginId = reader.GetString(reader.GetOrdinal("user_id")),
                                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                                Email = reader.GetString(reader.GetOrdinal("email")),
                                PasswordHash = reader.IsDBNull(reader.GetOrdinal("password_hash"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("password_hash")),
                                Role = reader.GetString(reader.GetOrdinal("role")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                LastLogin = reader.GetDateTime(reader.GetOrdinal("last_login")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            };
                        }
                    }
                }

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
                List<User> list = new List<User>();
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
SELECT
    id,
    user_id,
    first_name,
    last_name,
    email,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
FROM users
WHERE is_active = @active
ORDER BY id DESC;";

                    var cmd = db.GetCommand(sql);
                    db.AddParameter(cmd, "active", DbTypes.Types.Boolean).Value = active;
                    using (DbDataReader reader = await db.Execute(cmd))
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new User
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                UserLoginId = reader.GetString(reader.GetOrdinal("user_id")),
                                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                                Email = reader.GetString(reader.GetOrdinal("email")),
                                Role = reader.GetString(reader.GetOrdinal("role")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                LastLogin = reader.GetDateTime(reader.GetOrdinal("last_login")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            });
                        }
                    }
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string loginExistsSql = @"
SELECT EXISTS(
    SELECT 1 FROM users
    WHERE lower(user_id) = lower(@login_id)
) as has_login;";
                    var loginExistsCmd = db.GetCommand(loginExistsSql);
                    db.AddParameter(loginExistsCmd, "login_id", DbTypes.Types.String).Value = loginId;
                    bool loginExists = false;
                    using (DbDataReader loginReader = await db.Execute(loginExistsCmd))
                    {
                        await loginReader.ReadAsync();
                        loginExists = loginReader.GetBoolean(loginReader.GetOrdinal("has_login"));
                    }

                    if (loginExists)
                        return new ApiResponse<UserResponseDto> { Success = false, Message = "Login ID is already in use" };

                    string emailExistsSql = @"
SELECT EXISTS(
    SELECT 1 FROM users
    WHERE lower(email) = lower(@email)
) as has_email;";
                    var emailExistsCmd = db.GetCommand(emailExistsSql);
                    db.AddParameter(emailExistsCmd, "email", DbTypes.Types.String).Value = emailNorm;
                    bool emailExists = false;
                    using (DbDataReader emailReader = await db.Execute(emailExistsCmd))
                    {
                        await emailReader.ReadAsync();
                        emailExists = emailReader.GetBoolean(emailReader.GetOrdinal("has_email"));
                    }

                    if (emailExists)
                        return new ApiResponse<UserResponseDto> { Success = false, Message = "Email is already in use" };

                    var now = DateTime.UtcNow;
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                    var role = dto.Role?.Trim() ?? string.Empty;

                    string insertSql = @"
INSERT INTO users (
    user_id,
    first_name,
    last_name,
    email,
    password_hash,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
)
VALUES (
    @user_id,
    @first_name,
    @last_name,
    @email,
    @password_hash,
    @role,
    @is_active,
    @last_login,
    @created_at,
    @created_by,
    @modified_at,
    @modified_by
)
RETURNING
    id,
    user_id,
    first_name,
    last_name,
    email,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by;";

                    var insertCmd = db.GetCommand(insertSql);
                    db.AddParameter(insertCmd, "user_id", DbTypes.Types.String).Value = loginId;
                    db.AddParameter(insertCmd, "first_name", DbTypes.Types.String).Value = dto.FirstName?.Trim() ?? string.Empty;
                    db.AddParameter(insertCmd, "last_name", DbTypes.Types.String).Value = dto.LastName?.Trim() ?? string.Empty;
                    db.AddParameter(insertCmd, "email", DbTypes.Types.String).Value = email;
                    db.AddParameter(insertCmd, "password_hash", DbTypes.Types.String).Value = passwordHash;
                    db.AddParameter(insertCmd, "role", DbTypes.Types.String).Value = role;
                    db.AddParameter(insertCmd, "is_active", DbTypes.Types.Boolean).Value = dto.IsActive;
                    db.AddParameter(insertCmd, "last_login", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(insertCmd, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(insertCmd, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(insertCmd, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(insertCmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    User? u = null;
                    using (DbDataReader insertReader = await db.Execute(insertCmd))
                    {
                        if (await insertReader.ReadAsync())
                        {
                            u = new User
                            {
                                Id = insertReader.GetInt32(insertReader.GetOrdinal("id")),
                                UserLoginId = insertReader.GetString(insertReader.GetOrdinal("user_id")),
                                FirstName = insertReader.GetString(insertReader.GetOrdinal("first_name")),
                                LastName = insertReader.GetString(insertReader.GetOrdinal("last_name")),
                                Email = insertReader.GetString(insertReader.GetOrdinal("email")),
                                Role = insertReader.GetString(insertReader.GetOrdinal("role")),
                                IsActive = insertReader.GetBoolean(insertReader.GetOrdinal("is_active")),
                                LastLogin = insertReader.GetDateTime(insertReader.GetOrdinal("last_login")),
                                CreatedAt = insertReader.GetDateTime(insertReader.GetOrdinal("created_at")),
                                CreatedBy = insertReader.IsDBNull(insertReader.GetOrdinal("created_by")) ? null : insertReader.GetInt64(insertReader.GetOrdinal("created_by")),
                                ModifiedAt = insertReader.GetDateTime(insertReader.GetOrdinal("modified_at")),
                                ModifiedBy = insertReader.IsDBNull(insertReader.GetOrdinal("modified_by")) ? null : insertReader.GetInt64(insertReader.GetOrdinal("modified_by")),
                            };
                        }
                    }

                    if (u == null)
                        return new ApiResponse<UserResponseDto> { Success = false, Message = "Could not create user" };

                    var permMap = await LoadRolePermissionsMapAsync();
                    return new ApiResponse<UserResponseDto> { Success = true, Data = Map(u, PermissionsForRole(u, permMap)) };
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string selectSql = @"
SELECT
    id,
    user_id,
    first_name,
    last_name,
    email,
    password_hash,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by
FROM users
WHERE id = @id
LIMIT 1;";

                    var selectCmd = db.GetCommand(selectSql);
                    db.AddParameter(selectCmd, "id", DbTypes.Types.Integer).Value = id;

                    User? u = null;
                    using (DbDataReader reader = await db.Execute(selectCmd))
                    {
                        if (await reader.ReadAsync())
                        {
                            u = new User
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                UserLoginId = reader.GetString(reader.GetOrdinal("user_id")),
                                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                                Email = reader.GetString(reader.GetOrdinal("email")),
                                PasswordHash = reader.IsDBNull(reader.GetOrdinal("password_hash")) ? null : reader.GetString(reader.GetOrdinal("password_hash")),
                                Role = reader.GetString(reader.GetOrdinal("role")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                LastLogin = reader.GetDateTime(reader.GetOrdinal("last_login")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            };
                        }
                    }

                    if (u == null)
                        return new ApiResponse<UserResponseDto> { Success = false, Message = "User not found" };

                    if (!string.IsNullOrWhiteSpace(dto.FirstName)) u.FirstName = dto.FirstName;
                    if (!string.IsNullOrWhiteSpace(dto.LastName)) u.LastName = dto.LastName;
                    if (!string.IsNullOrWhiteSpace(dto.Email)) u.Email = dto.Email;
                    if (!string.IsNullOrWhiteSpace(dto.Role)) u.Role = dto.Role;
                    if (dto.IsActive.HasValue) u.IsActive = dto.IsActive.Value;

                    if (!string.IsNullOrWhiteSpace(dto.Password))
                    {
                        if (dto.Password.Length < 6)
                        {
                            return new ApiResponse<UserResponseDto>
                            {
                                Success = false,
                                Message = "Password must be at least 6 characters"
                            };
                        }
                        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                    }

                    u.ModifiedAt = DateTime.UtcNow;
                    u.ModifiedBy = AuditUserIds.System;

                    string updateSql = @"
UPDATE users
SET
    first_name = @first_name,
    last_name = @last_name,
    email = @email,
    role = @role,
    is_active = @is_active,
    password_hash = @password_hash,
    modified_at = @modified_at,
    modified_by = @modified_by
WHERE id = @id
RETURNING
    id,
    user_id,
    first_name,
    last_name,
    email,
    role,
    is_active,
    last_login,
    created_at,
    created_by,
    modified_at,
    modified_by;";

                    var updateCmd = db.GetCommand(updateSql);
                    db.AddParameter(updateCmd, "id", DbTypes.Types.Integer).Value = u.Id;
                    db.AddParameter(updateCmd, "first_name", DbTypes.Types.String).Value = u.FirstName;
                    db.AddParameter(updateCmd, "last_name", DbTypes.Types.String).Value = u.LastName;
                    db.AddParameter(updateCmd, "email", DbTypes.Types.String).Value = u.Email;
                    db.AddParameter(updateCmd, "role", DbTypes.Types.String).Value = u.Role;
                    db.AddParameter(updateCmd, "is_active", DbTypes.Types.Boolean).Value = u.IsActive;
                    db.AddParameter(updateCmd, "password_hash", DbTypes.Types.String).Value = u.PasswordHash ?? (object)DBNull.Value;
                    db.AddParameter(updateCmd, "modified_at", DbTypes.Types.DateTime).Value = u.ModifiedAt;
                    db.AddParameter(updateCmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    using (DbDataReader outReader = await db.Execute(updateCmd))
                    {
                        if (!await outReader.ReadAsync())
                            return new ApiResponse<UserResponseDto> { Success = false, Message = "User not found" };

                        u.Id = outReader.GetInt32(outReader.GetOrdinal("id"));
                        u.UserLoginId = outReader.GetString(outReader.GetOrdinal("user_id"));
                        u.FirstName = outReader.GetString(outReader.GetOrdinal("first_name"));
                        u.LastName = outReader.GetString(outReader.GetOrdinal("last_name"));
                        u.Email = outReader.GetString(outReader.GetOrdinal("email"));
                        u.Role = outReader.GetString(outReader.GetOrdinal("role"));
                        u.IsActive = outReader.GetBoolean(outReader.GetOrdinal("is_active"));
                        u.LastLogin = outReader.GetDateTime(outReader.GetOrdinal("last_login"));
                        u.CreatedAt = outReader.GetDateTime(outReader.GetOrdinal("created_at"));
                        u.CreatedBy = outReader.IsDBNull(outReader.GetOrdinal("created_by")) ? null : outReader.GetInt64(outReader.GetOrdinal("created_by"));
                        u.ModifiedAt = outReader.GetDateTime(outReader.GetOrdinal("modified_at"));
                        u.ModifiedBy = outReader.IsDBNull(outReader.GetOrdinal("modified_by")) ? null : outReader.GetInt64(outReader.GetOrdinal("modified_by"));
                    }

                    var permMap = await LoadRolePermissionsMapAsync();
                    return new ApiResponse<UserResponseDto> { Success = true, Data = Map(u, PermissionsForRole(u, permMap)) };
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string selectSql = @"SELECT 1 FROM users WHERE id=@id LIMIT 1;";
                    var selectCmd = db.GetCommand(selectSql);
                    db.AddParameter(selectCmd, "id", DbTypes.Types.Integer).Value = id;

                    bool exists;
                    using (DbDataReader reader = await db.Execute(selectCmd))
                    {
                        exists = await reader.ReadAsync();
                    }

                    if (!exists)
                        return new ApiResponse<bool> { Success = false, Message = "User not found" };

                    var cmd = db.GetCommand(@"
UPDATE users
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
                            return new ApiResponse<bool> { Success = false, Message = "User not found" };
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
