using Microsoft.Data.Sqlite;

var dbPath = @"C:\Projetos\metrics-simple\src\Api\config\config.db.backup";
var connString = $"Data Source={dbPath}";

using var conn = new SqliteConnection(connString);
conn.Open();

// Check auth_users
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT id, username, display_name, email, password_hash, is_active FROM auth_users LIMIT 1";
var reader = cmd.ExecuteReader();

if (reader.Read())
{
    Console.WriteLine("✓ User found in auth_users table");
    Console.WriteLine($"  - ID: {reader["id"]}");
    Console.WriteLine($"  - Username: {reader["username"]}");
    Console.WriteLine($"  - Display Name: {reader["display_name"]}");
    Console.WriteLine($"  - Email: {reader["email"] ?? "null"}");
    var hashStr = reader["password_hash"].ToString();
    Console.WriteLine($"  - Password Hash (BCrypt): {hashStr?.Substring(0, Math.Min(20, hashStr.Length))}...");
    Console.WriteLine($"  - Is Active: {reader["is_active"]}");
    
    var userId = reader["id"].ToString();
    reader.Close();
    
    // Check roles
    cmd.CommandText = "SELECT role FROM auth_user_roles WHERE user_id = @userId";
    cmd.Parameters.AddWithValue("@userId", userId);
    var roleReader = cmd.ExecuteReader();
    var roles = new List<string>();
    while (roleReader.Read())
        roles.Add(roleReader["role"].ToString());
    Console.WriteLine($"  - Roles: [{string.Join(", ", roles)}]");
}
else
{
    Console.WriteLine("✗ No users found");
}

conn.Close();
