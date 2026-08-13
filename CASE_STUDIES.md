# SqlMq Case Studies

The following case studies illustrate real-world scenarios where enterprises have adopted the `SqlMq` pattern in .NET to reduce costs, simplify architecture, and improve data consistency.

---

## Case Study 1: Resolving the Dual-Write Problem in E-Commerce
**Company:** GlobalRetail Inc. (B2C E-commerce Platform)

### The Challenge
GlobalRetail operated a .NET 8 microservice architecture using RabbitMQ. When a user placed an order, the system saved the order to Azure SQL and published an `OrderCreated` event to RabbitMQ. 
During high-traffic events (Black Friday), transient network timeouts to RabbitMQ caused the database transaction to succeed but the event to fail. This resulted in "ghost orders" where customers were charged, but the fulfillment center never received the event.

### The Solution: SqlMq
GlobalRetail replaced RabbitMQ with SqlMq for their internal microservice communication. 
By injecting `ISqlMqTemplate` directly into their Entity Framework Core transaction block, the `OrderCreated` event was written to the `sqlmq_messages` table atomically alongside the order data.

### The Results
- **100% Data Consistency:** The dual-write anomaly was entirely eliminated. Zero dropped messages during the following Black Friday.
- **Simplified Operations:** The DevOps team decommissioned a 3-node RabbitMQ cluster, reducing infrastructure costs by $1,200/month.

---

## Case Study 2: Infrastructure Consolidation for B2B SaaS
**Company:** FinTech Solutions LLC (Financial Reporting SaaS)

### The Challenge
FinTech Solutions offered a SaaS product deployed in isolated, single-tenant environments for compliance reasons. Each tenant required their own Azure App Service, Azure SQL database, and an Azure Service Bus namespace. 
As the company scaled to 500+ tenants, the fixed base costs and Terraform complexity of managing 500 separate Service Bus namespaces became unmanageable.

### The Solution: SqlMq
The architecture team realized that their background jobs (generating PDF reports, calculating daily aggregates) did not require the advanced pub/sub routing of Service Bus. They adopted SqlMq, migrating the background polling mechanisms directly into the tenant's existing Azure SQL databases.

### The Results
- **Cost Reduction:** Dropped the Azure Service Bus requirement entirely, saving $5,000+ monthly across all tenants.
- **Deployment Speed:** New tenant provisioning time dropped from 15 minutes to 3 minutes, as the Terraform scripts no longer needed to provision external messaging resources.

---

## Case Study 3: Dynamic Rate Limiting & API Circuit Breaking
**Company:** TravelSync (Flight Aggregator API)

### The Challenge
TravelSync heavily relies on calling legacy third-party airline APIs. These APIs are notoriously fragile and enforce strict, undocumented rate limits. When TravelSync sent too many requests, the airlines would temporarily IP-ban them, causing cascading failures.

### The Solution: SqlMq Exponential Backoff
TravelSync utilized SqlMq's `IHostedService` polling engine combined with its dynamic **Visibility Timeout** feature. 
When an airline API returned an HTTP 429 (Too Many Requests), the SqlMq consumer caught the exception and applied an exponential backoff formula. It updated the `VisibleAfter` column of the message to push it 5 minutes into the future, then 15 minutes, then 1 hour.

### The Results
- **Graceful Degradation:** The system automatically self-healed. When an airline API went down, messages quietly buffered in the SQL Server table without consuming CPU cycles or thread pools.
- **Observability:** By querying `SELECT COUNT(*) FROM sqlmq_messages WHERE VisibleAfter > GETDATE()`, operations teams built PowerBI dashboards to visually track API health and backpressure in real-time.
