namespace SqlMq.Abstractions;

/// <summary>
/// Provides methods to publish messages to the SQL-based message queue.
/// </summary>
public interface ISqlMqTemplate
{
    /// <summary>
    /// Enqueues a message payload to the specified queue.
    /// </summary>
    /// <typeparam name="T">The type of the message payload.</typeparam>
    /// <param name="queueName">The destination queue name.</param>
    /// <param name="payload">The message payload to serialize and enqueue.</param>
    /// <param name="delay">Optional delay before the message becomes visible to consumers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    Task SendAsync<T>(string queueName, T payload, TimeSpan? delay = null, CancellationToken cancellationToken = default);
}
