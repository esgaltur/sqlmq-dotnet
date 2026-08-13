using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlMq.Abstractions;
using SqlMq.Attributes;
using SqlMq.DependencyInjection;
using SqlMq.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace SqlMq.Tests;

// 1. Define the Payload
public record EmailMessage(string To, string Subject);

// 2. Define the Consumer
public class EmailService
{
    // A static flag just for the test to verify execution
    public static bool MessageReceived = false;
    public static EmailMessage? ReceivedPayload = null;

    [SqlMqListener("email_queue")]
    public Task HandleEmail(EmailMessage message)
    {
        MessageReceived = true;
        ReceivedPayload = message;
        
        // Processing the email...
        return Task.CompletedTask;
    }
}

public class SqlMqUsageExampleTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task End_To_End_Usage_Example()
    {
        // -------------------------------------------------------------
        // SETUP: Configure Dependency Injection (Just like Program.cs)
        // -------------------------------------------------------------
        var services = new ServiceCollection();
        
        // Add Logging
        services.AddLogging();
        
        // Add SqlMq and scan the current assembly for [SqlMqListener]
        services.AddSqlMq(options => 
        {
            options.ConnectionString = _msSqlContainer.GetConnectionString();
            options.AutoCreateSchema = true;
            options.DefaultPollInterval = TimeSpan.FromMilliseconds(100); // Fast for testing
        }, Assembly.GetExecutingAssembly());

        var serviceProvider = services.BuildServiceProvider();

        // -------------------------------------------------------------
        // START: Start the Background Polling Worker
        // -------------------------------------------------------------
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        var sqlMqWorker = hostedServices.OfType<SqlMqWorker>().Single();
        
        var cts = new CancellationTokenSource();
        // Start the worker in the background
        var workerTask = sqlMqWorker.StartAsync(cts.Token);

        // Give the worker a moment to initialize the schema
        await Task.Delay(1000);

        // -------------------------------------------------------------
        // PRODUCE: Send a message via ISqlMqTemplate
        // -------------------------------------------------------------
        var template = serviceProvider.GetRequiredService<ISqlMqTemplate>();
        
        var email = new EmailMessage("test@example.com", "Welcome to SqlMq!");
        await template.SendAsync("email_queue", email);

        // -------------------------------------------------------------
        // CONSUME: Wait and assert that the Listener was invoked
        // -------------------------------------------------------------
        
        // Wait for the background worker to poll and process the message
        int waitAttempts = 0;
        while (!EmailService.MessageReceived && waitAttempts < 50) // Wait up to 5 seconds
        {
            await Task.Delay(100);
            waitAttempts++;
        }

        // Cleanly stop the worker
        cts.Cancel();
        await sqlMqWorker.StopAsync(CancellationToken.None);

        // -------------------------------------------------------------
        // ASSERT: Verify Results
        // -------------------------------------------------------------
        Assert.True(EmailService.MessageReceived, "The SqlMqListener method was not invoked.");
        Assert.NotNull(EmailService.ReceivedPayload);
        Assert.Equal("test@example.com", EmailService.ReceivedPayload.To);
        Assert.Equal("Welcome to SqlMq!", EmailService.ReceivedPayload.Subject);
    }
}
