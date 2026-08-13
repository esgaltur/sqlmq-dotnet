using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;
using SqlMq.Abstractions;

namespace SqlMq.EntityFrameworkCore;

public interface ISqlMqEfCoreTemplate<TContext> where TContext : DbContext
{
    Task SendAsync<TPayload>(string queueName, TPayload payload, TimeSpan? delay = null, CancellationToken cancellationToken = default);
}

public class SqlMqEfCoreTemplate<TContext> : ISqlMqEfCoreTemplate<TContext> where TContext : DbContext
{
    private readonly TContext _dbContext;
    private readonly ISqlMqSerializer _serializer;

    public SqlMqEfCoreTemplate(TContext dbContext, ISqlMqSerializer serializer)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public async Task SendAsync<TPayload>(string queueName, TPayload payload, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        var serializedPayload = _serializer.Serialize(payload);
        
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO sqlmq_messages (QueueName, Payload, VisibleAfter) 
            VALUES (@QueueName, @Payload, DATEADD(millisecond, @DelayMs, SYSUTCDATETIME()));";

        // If there's an active transaction, bind the command to it
        if (_dbContext.Database.CurrentTransaction != null)
        {
            command.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();
        }

        var delayMs = delay?.TotalMilliseconds ?? 0;

        var pQueue = command.CreateParameter();
        pQueue.ParameterName = "@QueueName";
        pQueue.Value = queueName;
        command.Parameters.Add(pQueue);

        var pPayload = command.CreateParameter();
        pPayload.ParameterName = "@Payload";
        pPayload.Value = serializedPayload;
        command.Parameters.Add(pPayload);

        var pDelay = command.CreateParameter();
        pDelay.ParameterName = "@DelayMs";
        pDelay.Value = delayMs;
        command.Parameters.Add(pDelay);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
