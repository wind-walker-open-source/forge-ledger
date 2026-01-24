

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
- API key authentication via `X-API-KEY` header
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
| ttl | N | TTL timestamp for auto-expiration |

This supports a single-table pattern for:
- Job metadata (with optional webhook URL)
- Item state
- Aggregated counts
- Automatic cleanup via DynamoDB TTL (default 30 days)

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

**Request Body**
```json
{
  "jobType": "invoice-processing",
  "expectedCount": 100,
  "ttlDays": 30,
  "webhookUrl": "https://example.com/webhook"
}
```

**Response**
```json
{
  "jobId": "01HZX...",
  "status": "PENDING",
  "jobType": "invoice-processing",
  "expectedCount": 100,
  "webhookUrl": "https://example.com/webhook"
}
```

---

### Get Job Status
```
GET /jobs/{jobId}
```

**Response**
```json
{
  "jobId": "01HZX...",
  "status": "COMPLETED",
  "jobType": "invoice-processing",
  "expectedCount": 100,
  "completedCount": 98,
  "failedCount": 2,
  "createdAt": "2026-01-24T12:00:00Z",
  "completedAt": "2026-01-24T14:30:00Z",
  "webhookStatus": "SENT"
}
```

---

### List Items for a Job
```
GET /jobs/{jobId}/items?status=FAILED&limit=50&nextToken=...
```

Returns items with optional filtering by status (PENDING, PROCESSING, COMPLETED, FAILED) and pagination.

---

### Register Items for a Job
```
POST /jobs/{jobId}/items:register
```

Registers one or more items that must be processed for the job. Idempotent.

---

### Claim Item
```
POST /jobs/{jobId}/items/{itemId}:claim
```

Marks an item as actively being processed by a worker.

---

### Complete Item
```
POST /jobs/{jobId}/items/{itemId}:complete
```

Marks an item as successfully completed.

---

### Fail Item
```
POST /jobs/{jobId}/items/{itemId}:fail
```

Marks an item as failed (does not remove it from the ledger).

---

### Retry Failed Item
```
POST /jobs/{jobId}/items/{itemId}:retry
```

Resets a FAILED item back to PENDING so it can be claimed and processed again. Decrements the job's failedCount and reverts COMPLETED_WITH_FAILURES jobs to RUNNING.

---

## 🔐 Authentication

All endpoints (except `/`, `/health`, `/swagger/*`) require an API key via the `X-API-KEY` header.

The API key is loaded from (in order):
1. **appsettings.json**: `ForgeLedger:ApiKey`
2. **AWS Parameter Store**: `/ForgeLedger/API/Key` (fallback)

For local development, set the key in `appsettings.Development.json`:
```json
{
  "ForgeLedger": {
    "ApiKey": "your-dev-api-key"
  }
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

## 🛣 Roadmap

### ✅ Completed
- [x] API key authentication
- [x] Job expiration / TTL cleanup
- [x] Webhook callbacks on completion
- [x] Retry failed items
- [x] List items with status filter

### 🔜 Future Ideas
- [ ] Native SQS helper SDK
- [ ] EventBridge integration
- [ ] Metrics export (CloudWatch / OpenTelemetry)
- [ ] Batch retry for all failed items

---

## 🧔 Author

Built by **Lister Potter**  
Wind Walker Forge, LLC  
> Quality forged through architecture & design

---

## ⚖️ License

[MIT](../../../LICENSE)