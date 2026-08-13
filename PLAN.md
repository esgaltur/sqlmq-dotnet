# SqlMq Future Development Plan

This document outlines the detailed plan for tackling the remainder of the SqlMq roadmap to bring the library to a fully production-ready, enterprise-grade state.

## 📍 Phase 1: Complete Core Resiliency (Milestone 2)
### Next Immediate Target: Idempotency
Exactly-once processing is the holy grail of distributed systems.
*   **The Plan:** We will add an optional `MessageId` string to the `ISqlMqTemplate.SendAsync` method. 
*   **The Mechanics:** We'll create a `sqlmq_idempotency` table (with `MessageId` as the Primary Key). When sending a message, we do a transactional insert into the idempotency table. If it throws a Primary Key violation, we silently drop the duplicate message. 

## 📍 Phase 2: Performance & Throughput (Milestones 3 & 4)
### Target A: Bulk Consuming (`List<T>`)
*   **The Plan:** Enhance `[SqlMqListener]` to allow methods like `Task ProcessBatch(List<OrderEvent> events)`. The worker will use `SELECT TOP(BatchSize)` to grab multiple locked rows at once, massively reducing database round-trips for high-volume queues.
### Target B: System.Text.Json Source Generators
*   **The Plan:** Remove all reflection-based JSON serialization in favor of compile-time `[JsonSerializable]` contexts. This makes parsing completely allocation-free and Native AOT compliant.

## 📍 Phase 3: Observability & Health (Milestones 3 & 4)
### Target A: OpenTelemetry & Metrics
*   **The Plan:** Integrate `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics`. Every message sent/received will generate a distributed trace, and we will emit `sqlmq.queue.depth` metrics for Prometheus/Grafana to scrape.
### Target B: Microsoft Health Checks
*   **The Plan:** Create a new `SqlMq.HealthChecks` NuGet package that implements `IHealthCheck` to verify if the SQL Server is reachable and the background workers haven't crashed.

## 📍 Phase 4: The Developer Ecosystem (Milestones 3 & 5)
### Target A: .NET Aspire Integration
*   **The Plan:** Build `SqlMq.Hosting.Aspire` to allow modern cloud-native developers to inject SqlMq into their distributed architecture using `builder.AddSqlMq("sql")`.
### Target B: The Blazor Management UI
*   **The Plan:** Build a beautiful Web UI that connects to the SQL database. It will let users monitor queue depths in real-time, view poison pills in the Dead Letter Queue, and manually click "Replay" to push them back into the main queue.
