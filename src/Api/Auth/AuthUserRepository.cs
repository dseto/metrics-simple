using Microsoft.Data.Sqlite;

namespace Metrics.Api.Auth;

/// <summary>
/// Repository for auth users (LocalJwt mode)
/// </summary>
public interface IAuthUserRepository
{
    Task<AuthUser?> GetByUsernameAsync(string username);
    Task<AuthUser?> GetByIdAsync(string id);
    Task<List<AuthUser>> GetAllAsync();
    Task<AuthUser> CreateAsync(AuthUser user);
    Task UpdateAsync(AuthUser user);
    Task UpdateLoginAttemptAsync(string id, int failedAttempts, DateTime? lockoutUntilUtc, DateTime? lastLoginUtc);
    Task<bool> HasAnyAdminAsync();
}

/// <summary>
/// SQLite implementation of IAuthUserRepository
/// </summary>
public class AuthUserRepository : IAuthUserRepository
{
    private readonly string _dbPath;

    public AuthUserRepository(string dbPath)
    {
        _dbPath = dbPath;
    }

    private SqliteConnection GetConnection()
    {
        var connectionString = $"Data Source={_dbPath}";
        return new SqliteConnection(connectionString);
    }

    public async Task<AuthUser?> GetByUsernameAsync(string username)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        // Normalize username for case-insensitive search
        var normalizedUsername = username.Trim().ToLowerInvariant();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, username, display_name, email, password_hash, is_active, 
                   failed_attempts, lockout_until_utc, created_at_utc, updated_at_utc, last_login_utc
            FROM auth_users 
            WHERE LOWER(username) = @username";
        cmd.Parameters.AddWithValue("@username", normalizedUsername);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var user = MapUser(reader);
        await reader.CloseAsync();

        // Load roles
        var roles = await GetUserRolesAsync(conn, user.Id);
        return user with { Roles = roles };
    }

    public async Task<AuthUser?> GetByIdAsync(string id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, username, display_name, email, password_hash, is_active, 
                   failed_attempts, lockout_until_utc, created_at_utc, updated_at_utc, last_login_utc
            FROM auth_users 
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var user = MapUser(reader);
        await reader.CloseAsync();

        var roles = await GetUserRolesAsync(conn, user.Id);
        return user with { Roles = roles };
    }

    public async Task<List<AuthUser>> GetAllAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, username, display_name, email, password_hash, is_active, 
                   failed_attempts, lockout_until_utc, created_at_utc, updated_at_utc, last_login_utc
            FROM auth_users 
            ORDER BY username ASC";

        var users = new List<AuthUser>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(MapUser(reader));
        }
        await reader.CloseAsync();

        // Load roles for each user
        foreach (var user in users.ToList())
        {
            var roles = await GetUserRolesAsync(conn, user.Id);
            users[users.IndexOf(user)] = user with { Roles = roles };
        }

        return users;
    }

    public async Task<AuthUser> CreateAsync(AuthUser user)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = @"
                INSERT INTO auth_users (id, username, display_name, email, password_hash, is_active, 
                                        failed_attempts, lockout_until_utc, created_at_utc, updated_at_utc, last_login_utc)
                VALUES (@id, @username, @displayName, @email, @passwordHash, @isActive, 
                        @failedAttempts, @lockoutUntilUtc, @createdAtUtc, @updatedAtUtc, @lastLoginUtc)";

            cmd.Parameters.AddWithValue("@id", user.Id);
            cmd.Parameters.AddWithValue("@username", user.Username);
            cmd.Parameters.AddWithValue("@displayName", (object?)user.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@failedAttempts", user.FailedAttempts);
            cmd.Parameters.AddWithValue("@lockoutUntilUtc", user.LockoutUntilUtc?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAtUtc", user.CreatedAtUtc.ToString("O"));
            cmd.Parameters.AddWithValue("@updatedAtUtc", user.UpdatedAtUtc.ToString("O"));
            cmd.Parameters.AddWithValue("@lastLoginUtc", user.LastLoginUtc?.ToString("O") ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // Insert roles
            foreach (var role in user.Roles)
            {
                using var roleCmd = conn.CreateCommand();
                roleCmd.Transaction = (SqliteTransaction)tx;
                roleCmd.CommandText = "INSERT INTO auth_user_roles (user_id, role) VALUES (@userId, @role)";
                roleCmd.Parameters.AddWithValue("@userId", user.Id);
                roleCmd.Parameters.AddWithValue("@role", role);
                await roleCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return user;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(AuthUser user)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = @"
                UPDATE auth_users 
                SET username = @username, display_name = @displayName, email = @email, 
                    password_hash = @passwordHash, is_active = @isActive, 
                    failed_attempts = @failedAttempts, lockout_until_utc = @lockoutUntilUtc,
                    updated_at_utc = @updatedAtUtc, last_login_utc = @lastLoginUtc
                WHERE id = @id";

            cmd.Parameters.AddWithValue("@id", user.Id);
            cmd.Parameters.AddWithValue("@username", user.Username);
            cmd.Parameters.AddWithValue("@displayName", (object?)user.DisplayName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@failedAttempts", user.FailedAttempts);
            cmd.Parameters.AddWithValue("@lockoutUntilUtc", user.LockoutUntilUtc?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@updatedAtUtc", user.UpdatedAtUtc.ToString("O"));
            cmd.Parameters.AddWithValue("@lastLoginUtc", user.LastLoginUtc?.ToString("O") ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // Update roles: delete and re-insert
            using var deleteCmd = conn.CreateCommand();
            deleteCmd.Transaction = (SqliteTransaction)tx;
            deleteCmd.CommandText = "DELETE FROM auth_user_roles WHERE user_id = @userId";
            deleteCmd.Parameters.AddWithValue("@userId", user.Id);
            await deleteCmd.ExecuteNonQueryAsync();

            foreach (var role in user.Roles)
            {
                using var roleCmd = conn.CreateCommand();
                roleCmd.Transaction = (SqliteTransaction)tx;
                roleCmd.CommandText = "INSERT INTO auth_user_roles (user_id, role) VALUES (@userId, @role)";
                roleCmd.Parameters.AddWithValue("@userId", user.Id);
                roleCmd.Parameters.AddWithValue("@role", role);
                await roleCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateLoginAttemptAsync(string id, int failedAttempts, DateTime? lockoutUntilUtc, DateTime? lastLoginUtc)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE auth_users 
            SET failed_attempts = @failedAttempts, 
                lockout_until_utc = @lockoutUntilUtc,
                last_login_utc = @lastLoginUtc,
                updated_at_utc = @updatedAtUtc
            WHERE id = @id";

        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@failedAttempts", failedAttempts);
        cmd.Parameters.AddWithValue("@lockoutUntilUtc", lockoutUntilUtc?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lastLoginUtc", lastLoginUtc?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAtUtc", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasAnyAdminAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM auth_user_roles 
            WHERE role = @role";
        cmd.Parameters.AddWithValue("@role", AppRoles.Admin);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    private static async Task<List<string>> GetUserRolesAsync(SqliteConnection conn, string userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT role FROM auth_user_roles WHERE user_id = @userId ORDER BY role ASC";
        cmd.Parameters.AddWithValue("@userId", userId);

        var roles = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            roles.Add(reader.GetString(0));
        }
        return roles;
    }

    private static AuthUser MapUser(SqliteDataReader reader)
    {
        return new AuthUser
        {
            Id = reader.GetString(0),
            Username = reader.GetString(1),
            DisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
            Email = reader.IsDBNull(3) ? null : reader.GetString(3),
            PasswordHash = reader.GetString(4),
            IsActive = reader.GetInt32(5) == 1,
            FailedAttempts = reader.GetInt32(6),
            LockoutUntilUtc = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
            CreatedAtUtc = DateTime.Parse(reader.GetString(8)),
            UpdatedAtUtc = DateTime.Parse(reader.GetString(9)),
            LastLoginUtc = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10))
        };
    }
}
