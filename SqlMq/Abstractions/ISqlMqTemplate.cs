namespace SqlMq.Abstractions;

/// <summary>
/// Provides methods to publish messages to the SQL-based message queue.
/// </summary>
public interface ISqlMqTemplate
{
    /// <summary>
    /// Sends a message to the specified queue.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="queueName">The destination queue name.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="delay">Optional delay before the message becomes visible to consumers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync<T>(string queueName, T payload, TimeSpan? delay = null, CancellationToken cancellationToken = default);
}
