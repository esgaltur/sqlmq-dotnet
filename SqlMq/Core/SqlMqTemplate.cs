using Microsoft.Data.SqlClient;
using SqlMq.Abstractions;

namespace SqlMq.Core;

public class SqlMqTemplate : ISqlMqTemplate
{
    private readonly ISqlMqConnectionFactory _connectionFactory;
    private readonly ISqlMqSerializer _serializer;

    public SqlMqTemplate(ISqlMqConnectionFactory connectionFactory, ISqlMqSerializer serializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public async Task SendAsync<T>(string queueName, T payload, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var serializedPayload = _serializer.Serialize(payload);
        
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        
        command.CommandText = @"
            INSERT INTO sqlmq_messages (QueueName, Payload, VisibleAfter) 
            VALUES (@QueueName, @Payload, DATEADD(millisecond, @DelayMs, SYSUTCDATETIME()));";

        var delayMs = delay?.TotalMilliseconds ?? 0;

        command.Parameters.AddWithValue("@QueueName", queueName);
        command.Parameters.AddWithValue("@Payload", serializedPayload);
        command.Parameters.AddWithValue("@DelayMs", delayMs);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
