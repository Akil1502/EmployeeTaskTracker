using Microsoft.Data.SqlClient;

namespace EmployeeTaskTracker.Api.Data;

/// <summary>
/// Hands out open connections to the application database. Centralising this
/// keeps the connection string in one place and gives the repositories a single
/// seam to work against.
/// </summary>
public interface ISqlConnectionFactory
{
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing. Set it in appsettings.json - see the " +
                "Database Setup section of the README.");
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            // Do not leak the connection if opening it failed.
            await connection.DisposeAsync();
            throw;
        }
    }
}
