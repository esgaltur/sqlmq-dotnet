using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlMq.Abstractions;
using SqlMq.Models;

namespace SqlMq.Core;

public class SqlMqConnectionFactory : ISqlMqConnectionFactory
{
    private readonly SqlMqOptions _options;

    public SqlMqConnectionFactory(IOptions<SqlMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
