namespace SqlMq.Abstractions;

/// <summary>
/// Abstraction for serializing and deserializing message payloads.
/// </summary>
public interface ISqlMqSerializer
{
    string Serialize<T>(T payload);
    T? Deserialize<T>(string serializedPayload);
}
