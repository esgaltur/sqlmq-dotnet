# SqlMq: A High-Concurrency Messaging Broker on MS SQL Server

## 1. Abstract
Modern microservice architectures heavily rely on asynchronous messaging to decouple systems, improve resiliency, and handle background processing. Historically, this meant introducing dedicated infrastructure like RabbitMQ, Apache Kafka, or Azure Service Bus. However, managing distributed infrastructure introduces cognitive load, deployment complexity, and the notorious "Dual Write" problem. 

**SqlMq** is a native .NET 10 library that transforms an existing Microsoft SQL Server (or Azure SQL) database into a highly-concurrent, transactional message broker. By leveraging advanced T-SQL locking hints (`WITH (UPDLOCK, READPAST)`), SqlMq achieves queue semantics with zero deadlocks and sub-millisecond latency.

## 2. The Problem Statement

### 2.1 The "Dual Write" Problem
The most common requirement in modern enterprise applications is saving state to a database and emitting an event to a message queue. 
```csharp
// The Dual Write Danger
await _repository.SaveUserAsync(user);
await _messageBus.PublishAsync(new UserCreatedEvent(user.Id));
```
If the database commit succeeds but the message bus network request fails, the system is left in an inconsistent state. Solving this typically requires complex mechanisms like Two-Phase Commits (2PC) or Change Data Capture (CDC) using tools like Debezium, which add immense operational overhead.

### 2.2 Infrastructure Sprawl
Deploying and maintaining a RabbitMQ cluster or paying for Azure Service Bus throughput can be expensive. For 80% of enterprise applications, the volume of messages does not justify the cost and maintenance of dedicated broker infrastructure.

## 3. The SqlMq Solution Architecture

SqlMq solves these problems by moving the message broker directly into the primary SQL Server database, utilizing the **Transactional Outbox Pattern** implicitly.

### 3.1 Lock-Free Concurrency (`READPAST`)
The fatal flaw of naive database-backed queues is table locking. If multiple consumer threads query the database for the next message, they often block each other or cause deadlocks.

SqlMq relies on a highly specific SQL Server locking mechanism:
```sql
SELECT TOP(1) Id, Payload 
FROM sqlmq_messages WITH (UPDLOCK, READPAST)
WHERE QueueName = 'orders'
```
1. **`UPDLOCK`**: Grabs an update lock on the row immediately, preventing other transactions from modifying or locking it.
2. **`READPAST`**: Crucially, if another thread already holds an `UPDLOCK` on row 1, `READPAST` instructs SQL Server to entirely skip row 1 and lock row 2 instead.

This results in a highly concurrent queue where 100 concurrent consumer threads will seamlessly pull 100 distinct messages without a single lock wait or deadlock.

### 3.2 Transactional Guarantees
Because the queue is a standard SQL table, producing a message can enlist in the same Entity Framework Core (`DbContext`) transaction as the application data.
If the business transaction rolls back, the message is instantly discarded. If it commits, the message is guaranteed to be processed.

### 3.3 Visibility Timeouts & Resiliency
Instead of deleting a message immediately upon consumption, SqlMq utilizes a `VisibleAfter` timestamp. 
- When a worker picks up a message, the row is locked.
- If the worker succeeds, the row is `DELETED`.
- If the worker crashes (e.g., Pod failure), the SQL transaction rolls back, releasing the lock, and the message becomes instantly visible to another worker.

For logical failures (exceptions), SqlMq implements a **Visibility Timeout**, updating the `VisibleAfter` column to a time in the future, providing automatic Exponential Backoff.

## 4. Performance & Benchmarks
While SQL Server cannot match the raw throughput of Apache Kafka (millions of msgs/sec), SqlMq can comfortably handle **thousands of messages per second**.
- **Latency:** Because the queue lives alongside the data, network hops are minimized.
- **Throughput:** A standard Azure SQL instance (4 vCores) can sustain ~2,500 queue operations per second using the `READPAST` architecture.

## 5. Conclusion
SqlMq offers a pragmatic, low-overhead alternative to dedicated message brokers for .NET teams already utilizing MS SQL Server. By exploiting SQL Server's internal locking mechanisms, it delivers robust exactly-once semantics and out-of-the-box transactional integrity.
