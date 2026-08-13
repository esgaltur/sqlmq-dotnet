using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace SqlMq.Abstractions;

/// <summary>
/// Factory for creating SQL Server connections to ensure clean dependency management.
/// </summary>
public interface ISqlMqConnectionFactory
{
    /// <summary>
    /// Creates and opens a new SQL connection.
    /// </summary>
    Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
