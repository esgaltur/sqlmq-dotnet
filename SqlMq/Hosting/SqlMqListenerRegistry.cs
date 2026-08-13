using System.Reflection;

namespace SqlMq.Hosting;

/// <summary>
/// Holds information about registered message consumers.
/// </summary>
public class SqlMqListenerRegistry
{
    private readonly Dictionary<string, List<ConsumerRegistration>> _consumers = new();

    public IReadOnlyDictionary<string, List<ConsumerRegistration>> Consumers => _consumers;

    public void Register(string queue, Type declaringType, MethodInfo method, Type payloadType, int maxRetries)
    {
        if (!_consumers.ContainsKey(queue))
        {
            _consumers[queue] = new List<ConsumerRegistration>();
        }

        _consumers[queue].Add(new ConsumerRegistration(declaringType, method, payloadType, maxRetries));
    }
}

public record ConsumerRegistration(Type DeclaringType, MethodInfo Method, Type PayloadType, int MaxRetries);
