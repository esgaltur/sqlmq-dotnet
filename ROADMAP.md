# SqlMq for .NET Roadmap

This document outlines the planned architecture, features, and milestones for the `SqlMq` project targeting .NET 10.

## 🎯 Current Status: v0.2.0-beta (Core Resiliency Complete)
The project is currently in the architectural design phase. The core mechanisms rely heavily on `Microsoft.Data.SqlClient` and MS SQL Server's native `READPAST` and `UPDLOCK` hints to provide safe concurrent polling.

---

## 🛣️ Upcoming Milestones

### Milestone 1: Core Polling & Concurrency (v0.1.0)
*Focus: Establish the foundational database schema, background service, and robust lock-free polling.*
- [x] **Schema Auto-Provisioning:** Implement an initializer that runs `CREATE TABLE` scripts for the main queue and idempotency tracking.
- [x] **The Polling Engine:** Build an `IHostedService` that continuously executes `SELECT TOP(X) ... WITH (UPDLOCK, READPAST)` queries.
- [x] **`ISqlMqTemplate` interface:** Basic `.SendAsync()` methods to serialize payloads via `System.Text.Json` and `INSERT` them.
- [x] **`[SqlMqListener]` Attribute:** Reflection and source-generator logic to discover consumer methods and bind them to the polling engine.

### Milestone 2: Transactional Outbox & Resiliency (v0.2.0)
*Focus: Ensure exactly-once delivery and EF Core integration.*
- [x] **EF Core Integration:** Provide an extension method (e.g., `services.AddSqlMqEntityFrameworkCore()`) to automatically enlist message sending into the current `DbContext` transaction.
- [ ] **Idempotency Repository:** Introduce a secondary tracking table to deduplicate messages via their unique Message ID.
- [x] **Dead Letter Queue (DLQ):** Automatically move messages to a `sqlmq_messages_dlq` table when `MaxRetries` is exhausted.
- [x] **Delayed Messaging:** Implement a `VisibleAfter` column to naturally support scheduled messages.

### Milestone 3: Modern .NET 10 Features (v0.3.0)
*Focus: Take full advantage of the .NET 10 ecosystem.*
- [ ] **Native AOT Support:** Replace reflection-based attribute scanning with a .NET Roslyn Source Generator to wire up consumers at compile-time.
- [ ] **OpenTelemetry Integration:** Emit standard OTel metrics (`sqlmq.queue.depth`, `sqlmq.consumer.latency`) and spans for distributed tracing.
- [ ] **System.Text.Json Source Generators:** Ensure payload serialization relies purely on generated contexts for allocation-free parsing.

### Milestone 4: Production Tooling (v1.0.0)
*Focus: Dashboard and management utilities.*
- [ ] **Blazor Dashboard:** A standalone ASP.NET Core UI or extension to monitor queue depths, replay DLQ messages, and view worker health.
- [ ] **Performance Benchmarks:** Compare throughput against Azure Service Bus and MassTransit with RabbitMQ to validate the MS SQL architecture.
