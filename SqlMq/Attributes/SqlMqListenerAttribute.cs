using System;

namespace SqlMq.Attributes;

/// <summary>
/// Marks a method as a consumer for a specific SQL message queue.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class SqlMqListenerAttribute : Attribute
{
    /// <summary>
    /// The name of the queue to poll.
    /// </summary>
    public string Queue { get; }

    /// <summary>
    /// Maximum number of retries before moving the message to the DLQ (Dead Letter Queue).
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    public SqlMqListenerAttribute(string queue)
    {
        if (string.IsNullOrWhiteSpace(queue))
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queue));
            
        Queue = queue;
    }
}
