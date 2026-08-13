using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace SqlMq.Tests;

public class SqlMqIntegrationTests : IAsyncLifetime
{
    // Spins up a real MS SQL Server 2022 instance in a Docker container
#pragma warning disable CS0618 // Type or member is obsolete
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();
#pragma warning restore CS0618 // Type or member is obsolete

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();
        await InitializeSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await _msSqlContainer.DisposeAsync();
    }

    private async Task InitializeSchemaAsync()
    {
        // Setup the initial queue table schema for the test
        var schemaSql = @"
            CREATE TABLE sqlmq_messages (
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                QueueName NVARCHAR(128) NOT NULL,
                Payload NVARCHAR(MAX) NOT NULL,
                EnqueuedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                VisibleAfter DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
            );
            CREATE INDEX IX_sqlmq_messages_queue ON sqlmq_messages (QueueName, VisibleAfter);
        ";

        await using var connection = new SqlConnection(_msSqlContainer.GetConnectionString());
        await connection.ExecuteAsync(schemaSql);
    }

    [Fact]
    public async Task Can_Insert_And_Read_Message_Using_ReadPast_Lock()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        
        var payload = "{\"OrderId\": 123}";
        await connection.ExecuteAsync("INSERT INTO sqlmq_messages (QueueName, Payload) VALUES (@QueueName, @Payload)",
            new { QueueName = "order_queue", Payload = payload });

        // Act - Simulate a consumer polling with UPDLOCK and READPAST
        var pollSql = @"
            BEGIN TRANSACTION;
            
            SELECT TOP(1) Id, Payload 
            FROM sqlmq_messages WITH (UPDLOCK, READPAST)
            WHERE QueueName = @QueueName AND VisibleAfter <= SYSUTCDATETIME()
            ORDER BY Id ASC;
            
            -- We don't commit yet, holding the lock
        ";

        var command = new SqlCommand(pollSql, connection);
        command.Parameters.AddWithValue("@QueueName", "order_queue");
        await connection.OpenAsync();
        
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(reader.Read());
        
        var msgId = reader.GetInt64(0);
        var msgPayload = reader.GetString(1);
        
        Assert.Equal(payload, msgPayload);
        
        // Assert - A second consumer polling concurrently should not see the locked message
        await using var connection2 = new SqlConnection(connectionString);
        await connection2.OpenAsync();
        
        var pollSql2 = @"
            SELECT TOP(1) Id, Payload 
            FROM sqlmq_messages WITH (UPDLOCK, READPAST)
            WHERE QueueName = @QueueName AND VisibleAfter <= SYSUTCDATETIME()
            ORDER BY Id ASC;
        ";
        
        var command2 = new SqlCommand(pollSql2, connection2);
        command2.Parameters.AddWithValue("@QueueName", "order_queue");
        using var reader2 = await command2.ExecuteReaderAsync();
        
        // Should be false because the first consumer has the lock and there's no other message
        Assert.False(reader2.Read()); 
    }
}
