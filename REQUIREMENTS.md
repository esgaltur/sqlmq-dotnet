# Requirements

This document outlines the system, software, and dependency requirements for using or contributing to the `SqlMq` .NET library.

## 🖥️ System & Infrastructure Requirements

### Database Server
- **Microsoft SQL Server:** SQL Server 2017 or higher. 
- **Cloud Support:** Fully compatible with Azure SQL Database, Azure SQL Managed Instance, and Amazon RDS for SQL Server.
- *Note:* No external plugins or custom CLR types are required. The entire mechanism relies on standard T-SQL tables and `WITH (UPDLOCK, READPAST)` query hints.

### .NET SDK
- **Version:** .NET 10 SDK or higher. 
- *Why?* The library is built from the ground up to leverage .NET 10's latest features, including Native AOT compatibility, advanced OpenTelemetry integration, and enhanced `System.Text.Json` source generators.

---

## 📦 Application Dependencies

To use this library in your application, your project must meet the following dependency baselines:

- **Microsoft.Extensions.Hosting:** Relying on `IHostedService` and `BackgroundService` for concurrent queue polling.
- **Microsoft.Data.SqlClient:** The core ADO.NET provider for SQL Server interactions.
- **System.Text.Json:** Used for high-performance, allocation-free serialization and deserialization of message payloads.

### Optional Integrations
- **Entity Framework Core (EF Core):** Seamless integration with EF Core `IDbContextTransaction` for the Transactional Outbox pattern.
- **OpenTelemetry (.NET):** For distributed tracing and metrics via `System.Diagnostics.Activity` and `System.Diagnostics.Metrics`.

---

## 🛠️ Development & Testing Requirements

If you wish to contribute to the source code, you will need:

- **IDE:** Visual Studio 2022 (latest preview), JetBrains Rider, or VS Code with the C# Dev Kit.
- **Testing:** `xUnit` and `Moq` for unit tests.
- **Integration Testing:** **Testcontainers for .NET** is used to spin up a real SQL Server instance (`mcr.microsoft.com/mssql/server`) during the test phase to validate transactional behavior and concurrent locking mechanics.
