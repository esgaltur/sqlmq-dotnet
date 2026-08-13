namespace SqlMq.Models;

public class SqlMqOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public bool AutoCreateSchema { get; set; } = true;
    public TimeSpan DefaultVisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan DefaultPollInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
