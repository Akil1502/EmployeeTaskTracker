using Microsoft.Data.SqlClient;

namespace EmployeeTaskTracker.Api.Data;

/// <summary>
/// Small helpers for mapping a <see cref="SqlDataReader"/> row onto a DTO.
/// Reading by column name keeps the mapping readable and means a stored
/// procedure can gain a column without breaking existing ordinals.
/// </summary>
internal static class SqlDataReaderExtensions
{
    public static string GetString(this SqlDataReader reader, string column)
        => reader.GetString(reader.GetOrdinal(column));

    public static int GetInt32(this SqlDataReader reader, string column)
        => reader.GetInt32(reader.GetOrdinal(column));

    public static bool GetBoolean(this SqlDataReader reader, string column)
        => reader.GetBoolean(reader.GetOrdinal(column));

    public static DateTime GetDateTime(this SqlDataReader reader, string column)
        => reader.GetDateTime(reader.GetOrdinal(column));

    public static string? GetNullableString(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int? GetNullableInt32(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static DateTime? GetNullableDateTime(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    /// <summary>
    /// Converts a null .NET value into <see cref="DBNull.Value"/> so it can be
    /// passed straight to a parameter collection.
    /// </summary>
    public static object AsDbValue(this object? value) => value ?? DBNull.Value;
}
