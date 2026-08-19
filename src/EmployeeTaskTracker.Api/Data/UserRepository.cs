using System.Data;
using EmployeeTaskTracker.Shared;
using Microsoft.Data.SqlClient;

namespace EmployeeTaskTracker.Api.Data;

/// <summary>A user row including the stored hash. Stays inside the API layer.</summary>
public sealed class UserRecord
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public interface IUserRepository
{
    Task<UserRecord?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetEmployeesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetAdminsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// User data access. Every call goes through a stored procedure - there is no
/// inline SQL and no ORM anywhere in this class.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UserRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<UserRecord?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_User_GetByEmail", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new UserRecord
        {
            UserId = reader.GetInt32("UserId"),
            Name = reader.GetString("Name"),
            Email = reader.GetString("Email"),
            PasswordHash = reader.GetString("PasswordHash"),
            Role = reader.GetString("Role"),
            IsActive = reader.GetBoolean("IsActive")
        };
    }

    public async Task<UserDto?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_User_GetById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    public Task<IReadOnlyList<UserDto>> GetEmployeesAsync(CancellationToken cancellationToken = default)
        => GetByProcedureAsync("dbo.usp_User_GetEmployees", cancellationToken);

    /// <summary>Recipients for the status-change notification.</summary>
    public Task<IReadOnlyList<UserDto>> GetAdminsAsync(CancellationToken cancellationToken = default)
        => GetByProcedureAsync("dbo.usp_User_GetAdmins", cancellationToken);

    private async Task<IReadOnlyList<UserDto>> GetByProcedureAsync(
        string procedureName, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var users = new List<UserDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            users.Add(MapUser(reader));

        return users;
    }

    private static UserDto MapUser(SqlDataReader reader) => new()
    {
        UserId = reader.GetInt32("UserId"),
        Name = reader.GetString("Name"),
        Email = reader.GetString("Email"),
        Role = reader.GetString("Role"),
        // usp_User_GetEmployees only returns active users and omits the column.
        IsActive = !reader.HasColumn("IsActive") || reader.GetBoolean("IsActive")
    };
}

internal static class SqlDataReaderColumnExtensions
{
    /// <summary>True when the current result set exposes the named column.</summary>
    public static bool HasColumn(this SqlDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
