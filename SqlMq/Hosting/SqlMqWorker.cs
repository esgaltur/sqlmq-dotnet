using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMq.Abstractions;
using SqlMq.Models;
using Microsoft.Data.SqlClient;

namespace SqlMq.Hosting;

public class SqlMqWorker : BackgroundService
{
    private readonly ISqlMqConnectionFactory _connectionFactory;
    private readonly SqlMqOptions _options;
    private readonly SqlMqListenerRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SqlMqWorker> _logger;

    public SqlMqWorker(
        ISqlMqConnectionFactory connectionFactory,
        IOptions<SqlMqOptions> options,
        SqlMqListenerRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<SqlMqWorker> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SqlMqWorker starting...");

        if (_options.AutoCreateSchema)
        {
            await InitializeSchemaAsync(stoppingToken);
        }

        if (_registry.Consumers.Count == 0)
        {
            _logger.LogWarning("No [SqlMqListener] methods were discovered. Worker is idle.");
            return;
        }

        var pollingTasks = new List<Task>();
        foreach (var queueEntry in _registry.Consumers)
        {
            var queueName = queueEntry.Key;
            var consumers = queueEntry.Value;
            
            pollingTasks.Add(Task.Run(() => PollQueueLoopAsync(queueName, consumers, stoppingToken), stoppingToken));
            _logger.LogInformation("Started polling task for queue: {QueueName} with {ConsumerCount} consumers.", queueName, consumers.Count);
        }

        await Task.WhenAll(pollingTasks);
        _logger.LogInformation("SqlMqWorker stopping.");
    }

    private async Task PollQueueLoopAsync(string queueName, List<ConsumerRegistration> consumers, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await TryProcessNextMessageAsync(queueName, consumers, stoppingToken);
                
                if (!processed)
                {
                    await Task.Delay(_options.DefaultPollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while polling queue {QueueName}.", queueName);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> TryProcessNextMessageAsync(string queueName, List<ConsumerRegistration> consumers, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT TOP(1) Id, Payload, RetryCount 
            FROM sqlmq_messages WITH (UPDLOCK, READPAST)
            WHERE QueueName = @QueueName AND VisibleAfter <= SYSUTCDATETIME()
            ORDER BY Id ASC;";
            
        command.Parameters.AddWithValue("@QueueName", queueName);

        long? messageId = null;
        string? payloadStr = null;
        int retryCount = 0;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                messageId = reader.GetInt64(0);
                payloadStr = reader.GetString(1);
                retryCount = reader.GetInt32(2);
            }
        }

        if (!messageId.HasValue || payloadStr == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        try
        {
            _logger.LogInformation("Processing message {MessageId} (Retry: {RetryCount}) from {QueueName}.", messageId, retryCount, queueName);
            
            using var scope = _serviceProvider.CreateScope();
            var serializer = scope.ServiceProvider.GetRequiredService<ISqlMqSerializer>();
            
            foreach (var consumer in consumers)
            {
                var instance = scope.ServiceProvider.GetRequiredService(consumer.DeclaringType);
                var deserializeMethod = typeof(ISqlMqSerializer).GetMethod(nameof(ISqlMqSerializer.Deserialize))!
                                        .MakeGenericMethod(consumer.PayloadType);
                                        
                var payloadObject = deserializeMethod.Invoke(serializer, new object[] { payloadStr });
                
                var result = consumer.Method.Invoke(instance, new[] { payloadObject });
                if (result is Task task)
                {
                    await task;
                }
            }
            
            await using var deleteCmd = connection.CreateCommand();
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM sqlmq_messages WHERE Id = @Id;";
            deleteCmd.Parameters.AddWithValue("@Id", messageId.Value);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
            
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}.", messageId);
            
            // Get Max Retries from the consumer mapping (assume all consumers on the queue share the highest max retries or just use the first)
            int maxRetries = consumers.Max(c => c.MaxRetries);

            await using var failureCmd = connection.CreateCommand();
            failureCmd.Transaction = transaction;

            if (retryCount >= maxRetries)
            {
                // Route to DLQ
                _logger.LogWarning("Message {MessageId} reached max retries ({MaxRetries}). Moving to DLQ.", messageId, maxRetries);
                failureCmd.CommandText = @"
                    INSERT INTO sqlmq_messages_dlq (OriginalId, QueueName, Payload, ErrorMessage) 
                    VALUES (@Id, @QueueName, @Payload, @ErrorMessage);
                    DELETE FROM sqlmq_messages WHERE Id = @Id;
                ";
                failureCmd.Parameters.AddWithValue("@ErrorMessage", ex.ToString());
            }
            else
            {
                // Exponential Backoff: delay = 2^retryCount * 5 seconds
                var delaySeconds = Math.Pow(2, retryCount) * 5;
                _logger.LogInformation("Backing off message {MessageId} for {Delay} seconds.", messageId, delaySeconds);
                
                failureCmd.CommandText = @"
                    UPDATE sqlmq_messages 
                    SET RetryCount = RetryCount + 1,
                        VisibleAfter = DATEADD(second, @DelaySeconds, SYSUTCDATETIME())
                    WHERE Id = @Id;
                ";
                failureCmd.Parameters.AddWithValue("@DelaySeconds", (int)delaySeconds);
            }

            failureCmd.Parameters.AddWithValue("@Id", messageId.Value);
            failureCmd.Parameters.AddWithValue("@QueueName", queueName);
            failureCmd.Parameters.AddWithValue("@Payload", payloadStr);

            await failureCmd.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return true; 
        }
    }

    private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='sqlmq_messages' and xtype='U')
                BEGIN
                    CREATE TABLE sqlmq_messages (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        QueueName NVARCHAR(128) NOT NULL,
                        Payload NVARCHAR(MAX) NOT NULL,
                        RetryCount INT NOT NULL DEFAULT 0,
                        EnqueuedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                        VisibleAfter DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                    CREATE INDEX IX_sqlmq_messages_queue ON sqlmq_messages (QueueName, VisibleAfter);
                END

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='sqlmq_messages_dlq' and xtype='U')
                BEGIN
                    CREATE TABLE sqlmq_messages_dlq (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        OriginalId BIGINT NOT NULL,
                        QueueName NVARCHAR(128) NOT NULL,
                        Payload NVARCHAR(MAX) NOT NULL,
                        ErrorMessage NVARCHAR(MAX) NULL,
                        MovedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END
            ";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SqlMq schema.");
        }
    }
}
