

# ForgeLedger

**ForgeLedger** is a lightweight, deterministic job and workflow ledger for queue‑driven and distributed systems.  
It provides a simple HTTP API for registering jobs, tracking work items, and determining when all processing has completed — making it ideal for coordinating long‑running batch operations (e.g., SQS fan‑out/fan‑in workflows).

> Part of the **Wind Walker Forge** ecosystem — tools designed for clarity, correctness, and operational integrity.

---

## ✨ Key Concepts

- **Job** – A logical unit of work (e.g., "Process all invoices for 2026‑01‑15").
- **Item** – A single work unit within a job (e.g., "Process invoice #12345").
- **Ledger** – A durable record (stored in DynamoDB) tracking state transitions for auditability and correctness.

ForgeLedger is intentionally simple:
- No authentication (yet)  
- No queue coupling (works with SQS, SNS, Step Functions, cron jobs, etc.)  
- No opinionated orchestration engine  

It focuses on **state, determinism, and visibility**.

---

## 🏗 Architecture Overview

- **Runtime:** AWS Lambda (.NET 8, Minimal APIs)
- **API:** Amazon API Gateway (REST)
- **Storage:** DynamoDB (single-table design)
- **Documentation:** Swagger UI exposed at `/swagger`

```
Client / Worker
    ↓ HTTP
API Gateway
    ↓
ForgeLedger (Lambda / .NET API)
    ↓
DynamoDB (ForgeLedger table)
```

---

## 📦 DynamoDB Table

The stack provisions a DynamoDB table named:

```
ForgeLedger
```

Schema:

| Attribute | Type | Purpose |
|--------|------|---------|
| PK | S | Partition key (e.g., JOB#<jobId>) |
| SK | S | Sort key (e.g., META, ITEM#<itemId>) |

This supports a single-table pattern for:
- Job metadata
- Item state
- Aggregated counts

---

## 🚀 API Endpoints

Base path (after deploy):

```
https://<api-id>.execute-api.<region>.amazonaws.com/Prod
```

### Health
```
GET /health
```

### Create Job
```
POST /jobs
```

**Response**
```json
{
  "jobId": "01HZX..."
}
```

---

### Register Items for a Job
```
POST /jobs/{jobId}/items:register
```

Registers one or more items that must be processed for the job.

---

### Claim Item
```
POST /jobs/{jobId}/items/{itemId}/claim
```

Marks an item as actively being processed by a worker.

---

### Complete Item
```
POST /jobs/{jobId}/items/{itemId}/complete
```

Marks an item as successfully completed.

---

### Fail Item
```
POST /jobs/{jobId}/items/{itemId}/fail
```

Marks an item as failed (does not remove it from the ledger).

---

### Get Job Status
```
GET /jobs/{jobId}
```

Typical response:

```json
{
  "jobId": "01HZX...",
  "total": 1500,
  "completed": 1500,
  "failed": 2,
  "inProgress": 0,
  "isComplete": true
}
```

---

## 📖 Swagger UI

Interactive documentation is available at:

```
GET /swagger
```

Example:
```
https://<api-id>.execute-api.<region>.amazonaws.com/Prod/swagger
```

---

## 🧪 Local Development

Run locally with:

```bash
dotnet run
```

Then open:

```
https://localhost:<port>/swagger
```

---

## ☁️ Deployment

This project uses the AWS .NET Lambda serverless template.

Deploy using:

```bash
dotnet lambda deploy-serverless
```

You will be prompted for:
- Stack name (e.g., `forge-ledger`)
- AWS region
- AWS profile

CloudFormation provisions:
- API Gateway
- Lambda function
- DynamoDB table (`ForgeLedger`)
- IAM permissions

---

## 🧭 Design Goals

ForgeLedger is intentionally designed around:

- **Determinism over convenience**
- **Auditability over magic**
- **Explicit state over implicit behavior**
- **Operational clarity over abstraction**

This makes it suitable for:
- Financial batch processing
- Payment orchestration
- Idempotent queue processing
- Distributed worker coordination
- Long‑running fan‑out workflows

---

## 🛣 Roadmap (Early Ideas)

- [ ] Auth / API keys or IAM integration
- [ ] Job expiration / TTL cleanup
- [ ] Native SQS helper SDK
- [ ] Webhook callbacks on completion
- [ ] EventBridge integration
- [ ] Metrics export (CloudWatch / OpenTelemetry)

---

## 🧔 Author

Built by **Lister Potter**  
Wind Walker Forge, LLC  
> Quality forged through architecture & design

---

## ⚖️ License

TBD (likely MIT once stabilized)