using System.Text.Json;
using SqlMq.Abstractions;

namespace SqlMq.Core;

public class SqlMqSerializer : ISqlMqSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Serialize<T>(T payload)
    {
        return JsonSerializer.Serialize(payload, _options);
    }

    public T? Deserialize<T>(string serializedPayload)
    {
        return JsonSerializer.Deserialize<T>(serializedPayload, _options);
    }
}
