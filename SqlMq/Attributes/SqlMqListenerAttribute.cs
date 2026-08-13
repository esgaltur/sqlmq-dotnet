using System;

namespace SqlMq.Attributes;

/// <summary>
/// Marks a method as a consumer for a specific SQL message queue.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class SqlMqListenerAttribute : Attribute
{
    /// <summary>
    /// The name of the queue to poll messages from.
    /// </summary>
    public string Queue { get; }

    /// <summary>
    /// The maximum number of times a message should be retried before being moved to the Dead Letter Queue.
    /// Defaults to 5.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlMqListenerAttribute"/> class.
    /// </summary>
    /// <param name="queue">The name of the queue.</param>
    public SqlMqListenerAttribute(string queue)
    {
        if (string.IsNullOrWhiteSpace(queue))
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queue));
            
        Queue = queue;
    }
}
