using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlMq.Abstractions;
using SqlMq.Attributes;
using SqlMq.DependencyInjection;

namespace SqlMq.DemoApp;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure SqlMq to connect to our local Docker SQL Server
                services.AddSqlMq(options =>
                {
                    options.ConnectionString = "Server=localhost,1433;Database=master;User Id=sa;Password=YourStrong(!)Password;TrustServerCertificate=True;";
                    options.AutoCreateSchema = true;
                    options.DefaultVisibilityTimeout = TimeSpan.FromSeconds(30);
                    options.DefaultPollInterval = TimeSpan.FromMilliseconds(500);
                }, typeof(Program).Assembly); // Scan this assembly for listeners
                
                // Register our simulated background producer
                services.AddHostedService<MessageProducerService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await host.RunAsync();
    }
}

// 1. The Consumer (Listener)
public class EmailWorker
{
    private readonly ILogger<EmailWorker> _logger;

    public EmailWorker(ILogger<EmailWorker> logger)
    {
        _logger = logger;
    }

    [SqlMqListener("email_queue")]
    public async Task SendEmailAsync(EmailRequest request)
    {
        _logger.LogInformation("=============================================");
        _logger.LogInformation("📨 RECEIVED MESSAGE: Sending email to {To}!", request.ToAddress);
        _logger.LogInformation("📧 Subject: {Subject}", request.Subject);
        _logger.LogInformation("=============================================");
        
        // Simulate work
        await Task.Delay(1000);
        
        // If this throws, SqlMq will retry it automatically based on MaxRetries
    }
}

// 2. The Producer (Background Service that generates messages)
public class MessageProducerService : BackgroundService
{
    private readonly ISqlMqTemplate _template;
    private readonly ILogger<MessageProducerService> _logger;

    public MessageProducerService(ISqlMqTemplate template, ILogger<MessageProducerService> logger)
    {
        _template = template;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the Host and database schema to initialize
        await Task.Delay(3000, stoppingToken);

        int counter = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            var emailRequest = new EmailRequest
            {
                ToAddress = $"user{counter}@example.com",
                Subject = $"Welcome to SqlMq! (Message #{counter})"
            };

            _logger.LogWarning("🚀 ENQUEUING: Message #{Counter}", counter);
            
            // Enqueue the message!
            await _template.SendAsync("email_queue", emailRequest);
            
            counter++;
            
            // Wait 5 seconds before sending the next one
            await Task.Delay(5000, stoppingToken);
        }
    }
}

public class EmailRequest
{
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}
