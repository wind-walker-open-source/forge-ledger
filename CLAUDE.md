# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

```bash
# Build entire solution
dotnet build

# Build for release
dotnet build --configuration Release

# Run the API locally (from solution root)
dotnet run --project ForgeLedger/src/ForgeLedger

# Deploy to AWS Lambda
dotnet lambda deploy-serverless
```

Local development serves Swagger UI at `https://localhost:<port>/swagger`.

## Project Architecture

ForgeLedger is a deterministic job and workflow ledger for queue-driven distributed systems. It tracks job/item state transitions with atomicity guarantees via DynamoDB.

### Solution Structure

- **ForgeLedger/** - Main AWS Lambda API (.NET 8 Minimal APIs)
- **ForgeLedger.Client/** - .NET client SDK (netstandard2.0)
- **ForgeLedger.Contracts/** - Shared request/response DTOs (netstandard2.0)

### Key Files

| Path | Purpose |
|------|---------|
| `ForgeLedger/src/ForgeLedger/Program.cs` | DI setup, middleware, Lambda hosting |
| `ForgeLedger/src/ForgeLedger/Api/Endpoints.cs` | REST endpoint definitions |
| `ForgeLedger/src/ForgeLedger/Core/IForgeLedgerStore.cs` | Store interface (6 methods) |
| `ForgeLedger/src/ForgeLedger/Stores/DynamoDbForgeLedgerStore.cs` | DynamoDB implementation with state machine |
| `ForgeLedger/src/ForgeLedger/serverless.template` | CloudFormation/SAM deployment template |

### State Machine

Items follow: `PENDING → PROCESSING → COMPLETED | FAILED`

Jobs follow: `RUNNING → COMPLETED | COMPLETED_WITH_FAILURES`

State transitions are enforced atomically via DynamoDB condition expressions.

### DynamoDB Single-Table Design

- Partition key: `PK` = `JOB#{jobId}`
- Sort key: `SK` = `META` (job metadata) or `ITEM#{itemId}` (item records)
- Table name configurable via `FORGELEDGER_TABLE` or `JOBS_TABLE` env vars, defaults to `ForgeLedger`

### API Endpoints

All item operations use colon-prefixed action syntax:
- `POST /jobs` - Create job
- `POST /jobs/{jobId}/items:register` - Register items (idempotent)
- `POST /jobs/{jobId}/items/{itemId}:claim` - Claim item for processing
- `POST /jobs/{jobId}/items/{itemId}:complete` - Mark complete
- `POST /jobs/{jobId}/items/{itemId}:fail` - Mark failed
- `GET /jobs/{jobId}` - Get job status

### Error Handling

- `ArgumentException` → 400 Bad Request
- `InvalidOperationException` → 409 Conflict
- All errors use RFC7807 Problem Details format