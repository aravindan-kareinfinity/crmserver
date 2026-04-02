using CRM.Server.DTOs;
using CRM.Server.Models;
using CRM.Server.Utils;
using System.Data.Common;

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
        IDbProvider dbprovider;

        public RoleService(IDbProvider dbprovider)
        {
            this.dbprovider = dbprovider;
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
            using (IDb db = await dbprovider.GetDb())
            {
                await db.Connect();
                string sql = @"SELECT role, COUNT(*) as cnt FROM users GROUP BY role;";
                var command = db.GetCommand(sql);
                using (DbDataReader reader = await db.Execute(command))
                {
                    var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadAsync())
                    {
                        var role = reader.GetString(reader.GetOrdinal("role"));
                        var cnt = reader.GetInt32(reader.GetOrdinal("cnt"));
                        map[role] = cnt;
                    }
                    return map;
                }
            }
        }

        public async Task<ApiResponse<List<RoleResponseDto>>> GetAll()
        {
            try
            {
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"SELECT id, name, description, permissions, created_at, created_by, modified_at, modified_by
FROM roles
ORDER BY name;";
                    var command = db.GetCommand(sql);
                    var roles = new List<Role>();
                    using (DbDataReader reader = await db.Execute(command))
                    {
                        while (await reader.ReadAsync())
                        {
                            var permissionsCsv = reader.IsDBNull(reader.GetOrdinal("permissions"))
                                ? ""
                                : reader.GetString(reader.GetOrdinal("permissions"));
                            roles.Add(new Role
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Name = reader.GetString(reader.GetOrdinal("name")),
                                Description = reader.GetString(reader.GetOrdinal("description")),
                                Permissions = string.IsNullOrWhiteSpace(permissionsCsv)
                                    ? new List<string>()
                                    : permissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            });
                        }
                    }

                    var counts = await UserCountsByRoleNameAsync();
                    var data = roles.Select(r => Map(r, counts.TryGetValue(r.Name, out var c) ? c : 0)).ToList();
                    return new ApiResponse<List<RoleResponseDto>> { Success = true, Data = data };
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"SELECT id, name, description, permissions, created_at, created_by, modified_at, modified_by
FROM roles
WHERE id = @id
LIMIT 1;";
                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "id", DbTypes.Types.Integer).Value = id;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role not found" };

                        var permissionsCsv = reader.IsDBNull(reader.GetOrdinal("permissions"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("permissions"));
                        var role = new Role
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Name = reader.GetString(reader.GetOrdinal("name")),
                            Description = reader.GetString(reader.GetOrdinal("description")),
                            Permissions = string.IsNullOrWhiteSpace(permissionsCsv)
                                ? new List<string>()
                                : permissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                            ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                            ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                        };

                        var counts = await UserCountsByRoleNameAsync();
                        var n = counts.TryGetValue(role.Name, out var c) ? c : 0;
                        return new ApiResponse<RoleResponseDto> { Success = true, Data = Map(role, n) };
                    }
                }
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
                var permissionsCsv = string.Join(",", dto.Permissions ?? new List<string>());

                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();
                    string sql = @"
INSERT INTO roles (
    name,
    description,
    permissions,
    user_count,
    created_at,
    created_by,
    modified_at,
    modified_by
)
VALUES (
    @name,
    @description,
    @permissions,
    @user_count,
    @created_at,
    @created_by,
    @modified_at,
    @modified_by
)
RETURNING
    id,
    name,
    description,
    permissions,
    created_at,
    created_by,
    modified_at,
    modified_by;";

                    var command = db.GetCommand(sql);
                    db.AddParameter(command, "name", DbTypes.Types.String).Value = name;
                    db.AddParameter(command, "description", DbTypes.Types.String).Value = dto.Description?.Trim() ?? string.Empty;
                    db.AddParameter(command, "permissions", DbTypes.Types.String).Value = permissionsCsv;
                    db.AddParameter(command, "user_count", DbTypes.Types.Integer).Value = 0;
                    db.AddParameter(command, "created_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(command, "created_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                    db.AddParameter(command, "modified_at", DbTypes.Types.DateTime).Value = now;
                    db.AddParameter(command, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    using (DbDataReader reader = await db.Execute(command))
                    {
                        if (!await reader.ReadAsync())
                            return new ApiResponse<RoleResponseDto> { Success = false, Message = "Create failed" };

                        var permissionsCsvOut = reader.GetString(reader.GetOrdinal("permissions"));
                        var r = new Role
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Name = reader.GetString(reader.GetOrdinal("name")),
                            Description = reader.GetString(reader.GetOrdinal("description")),
                            Permissions = string.IsNullOrWhiteSpace(permissionsCsvOut)
                                ? new List<string>()
                                : permissionsCsvOut.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                            ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                            ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                        };

                        return new ApiResponse<RoleResponseDto> { Success = true, Data = Map(r, 0) };
                    }
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string selectSql = @"SELECT id, name, description, permissions, created_at, created_by, modified_at, modified_by
FROM roles
WHERE id = @id
LIMIT 1;";
                    var selectCommand = db.GetCommand(selectSql);
                    db.AddParameter(selectCommand, "id", DbTypes.Types.Integer).Value = id;

                    Role? existingRole = null;
                    using (DbDataReader reader = await db.Execute(selectCommand))
                    {
                        if (await reader.ReadAsync())
                        {
                            var existingPermissionsCsv = reader.IsDBNull(reader.GetOrdinal("permissions"))
                                ? ""
                                : reader.GetString(reader.GetOrdinal("permissions"));
                            existingRole = new Role
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Name = reader.GetString(reader.GetOrdinal("name")),
                                Description = reader.GetString(reader.GetOrdinal("description")),
                                Permissions = string.IsNullOrWhiteSpace(existingPermissionsCsv)
                                    ? new List<string>()
                                    : existingPermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetInt64(reader.GetOrdinal("created_by")),
                                ModifiedAt = reader.GetDateTime(reader.GetOrdinal("modified_at")),
                                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("modified_by")) ? null : reader.GetInt64(reader.GetOrdinal("modified_by")),
                            };
                        }
                    }

                    if (existingRole == null)
                        return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role not found" };

                    var newName = dto.Name.Trim();
                    if (string.IsNullOrEmpty(newName))
                        return new ApiResponse<RoleResponseDto> { Success = false, Message = "Role name is required" };

                    var oldName = existingRole.Name;
                    var modifiedAt = DateTime.UtcNow;
                    var permissionsCsv = string.Join(",", dto.Permissions ?? new List<string>());

                    if (!string.Equals(oldName, newName, StringComparison.Ordinal))
                    {
                        string takenSql = @"SELECT 1 FROM roles WHERE name = @name AND id <> @id LIMIT 1;";
                        var takenCmd = db.GetCommand(takenSql);
                        db.AddParameter(takenCmd, "name", DbTypes.Types.String).Value = newName;
                        db.AddParameter(takenCmd, "id", DbTypes.Types.Integer).Value = id;

                        using (DbDataReader takenReader = await db.Execute(takenCmd))
                        {
                            if (await takenReader.ReadAsync())
                                return new ApiResponse<RoleResponseDto> { Success = false, Message = "A role with this name already exists" };
                        }

                        // Preserve the "system" audit intent when updating users for a renamed role.
                        string updateUsersSql = @"UPDATE users
SET role = @newName, modified_at = @modified_at, modified_by = @modified_by
WHERE role = @oldName;";
                        var updateUsersCmd = db.GetCommand(updateUsersSql);
                        db.AddParameter(updateUsersCmd, "newName", DbTypes.Types.String).Value = newName;
                        db.AddParameter(updateUsersCmd, "modified_at", DbTypes.Types.DateTime).Value = modifiedAt;
                        db.AddParameter(updateUsersCmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;
                        db.AddParameter(updateUsersCmd, "oldName", DbTypes.Types.String).Value = oldName;
                        await db.ExecuteNonQuery(updateUsersCmd);
                    }

                    string updateRoleSql = @"UPDATE roles
SET name = @name,
    description = @description,
    permissions = @permissions,
    modified_at = @modified_at,
    modified_by = @modified_by
WHERE id = @id
RETURNING id, name, description, permissions, created_at, created_by, modified_at, modified_by;";

                    var updateRoleCmd = db.GetCommand(updateRoleSql);
                    db.AddParameter(updateRoleCmd, "id", DbTypes.Types.Integer).Value = id;
                    db.AddParameter(updateRoleCmd, "name", DbTypes.Types.String).Value = newName;
                    db.AddParameter(updateRoleCmd, "description", DbTypes.Types.String).Value = dto.Description?.Trim() ?? string.Empty;
                    db.AddParameter(updateRoleCmd, "permissions", DbTypes.Types.String).Value = permissionsCsv;
                    db.AddParameter(updateRoleCmd, "modified_at", DbTypes.Types.DateTime).Value = modifiedAt;
                    db.AddParameter(updateRoleCmd, "modified_by", DbTypes.Types.Long).Value = AuditUserIds.System;

                    using (DbDataReader outReader = await db.Execute(updateRoleCmd))
                    {
                        if (!await outReader.ReadAsync())
                            return new ApiResponse<RoleResponseDto> { Success = false, Message = "Update failed" };

                        var permissionsCsvOut = outReader.IsDBNull(outReader.GetOrdinal("permissions"))
                            ? ""
                            : outReader.GetString(outReader.GetOrdinal("permissions"));

                        var updatedRole = new Role
                        {
                            Id = outReader.GetInt32(outReader.GetOrdinal("id")),
                            Name = outReader.GetString(outReader.GetOrdinal("name")),
                            Description = outReader.GetString(outReader.GetOrdinal("description")),
                            Permissions = string.IsNullOrWhiteSpace(permissionsCsvOut)
                                ? new List<string>()
                                : permissionsCsvOut.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                            CreatedAt = outReader.GetDateTime(outReader.GetOrdinal("created_at")),
                            CreatedBy = outReader.IsDBNull(outReader.GetOrdinal("created_by")) ? null : outReader.GetInt64(outReader.GetOrdinal("created_by")),
                            ModifiedAt = outReader.GetDateTime(outReader.GetOrdinal("modified_at")),
                            ModifiedBy = outReader.IsDBNull(outReader.GetOrdinal("modified_by")) ? null : outReader.GetInt64(outReader.GetOrdinal("modified_by")),
                        };

                        var counts = await UserCountsByRoleNameAsync();
                        var n = counts.TryGetValue(updatedRole.Name, out var c) ? c : 0;
                        return new ApiResponse<RoleResponseDto> { Success = true, Data = Map(updatedRole, n) };
                    }
                }
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
                using (IDb db = await dbprovider.GetDb())
                {
                    await db.Connect();

                    string selectSql = @"SELECT id, name FROM roles WHERE id = @id LIMIT 1;";
                    var selectCmd = db.GetCommand(selectSql);
                    db.AddParameter(selectCmd, "id", DbTypes.Types.Integer).Value = id;

                    string? roleName = null;
                    using (DbDataReader reader = await db.Execute(selectCmd))
                    {
                        if (await reader.ReadAsync())
                            roleName = reader.GetString(reader.GetOrdinal("name"));
                    }

                    if (roleName == null)
                        return new ApiResponse<bool> { Success = false, Message = "Role not found" };

                    string assignedSql = @"SELECT COUNT(*) as cnt FROM users WHERE role = @role;";
                    var assignedCmd = db.GetCommand(assignedSql);
                    db.AddParameter(assignedCmd, "role", DbTypes.Types.String).Value = roleName;

                    int assigned;
                    using (DbDataReader assignedReader = await db.Execute(assignedCmd))
                    {
                        await assignedReader.ReadAsync();
                        assigned = assignedReader.GetInt32(assignedReader.GetOrdinal("cnt"));
                    }

                    if (assigned > 0)
                        return new ApiResponse<bool>
                        {
                            Success = false,
                            Message = $"Cannot delete role assigned to {assigned} user(s). Reassign users first."
                        };

                    string deleteSql = @"DELETE FROM roles WHERE id = @id;";
                    var deleteCmd = db.GetCommand(deleteSql);
                    db.AddParameter(deleteCmd, "id", DbTypes.Types.Integer).Value = id;
                    await db.ExecuteNonQuery(deleteCmd);

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
