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

# Deploy with custom parameters
dotnet lambda deploy-serverless --template-parameters "ApiKey=my-secret-key"
```

Local development serves Swagger UI at `https://localhost:<port>/swagger`.

### Deployment Parameters

The `serverless.template` supports these optional CloudFormation parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `ApiKey` | *(auto-generated)* | API key value. If not provided, a secure 32-character key is auto-generated |
| `ApiKeySecretName` | `ForgeLedger/API/Key` | Secrets Manager secret name for the API key |
| `BaseUrlParameterPath` | `/ForgeLedger/API/BaseUrl` | SSM Parameter Store path for the Base URL (always created) |

Example deployments:
```bash
# Deploy with auto-generated API key (simplest)
dotnet lambda deploy-serverless

# Deploy with specific API key
dotnet lambda deploy-serverless --template-parameters "ApiKey=my-secret-key"

# Deploy with custom paths (multi-environment)
dotnet lambda deploy-serverless \
  --template-parameters "ApiKeySecretName=prod/forge/apikey;BaseUrlParameterPath=/prod/forge/baseurl"
```

The Base URL is automatically stored in Parameter Store after deployment for client discovery.

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
| `ForgeLedger/src/ForgeLedger/Auth/` | API key middleware and provider |
| `ForgeLedger/src/ForgeLedger/Core/IForgeLedgerStore.cs` | Store interface (8 methods) |
| `ForgeLedger/src/ForgeLedger/Stores/DynamoDbForgeLedgerStore.cs` | DynamoDB implementation with state machine |
| `ForgeLedger/src/ForgeLedger/serverless.template` | CloudFormation/SAM deployment template |

### State Machine

Items follow: `PENDING → PROCESSING → COMPLETED | FAILED`
- Failed items can be retried: `FAILED → PENDING` (via `:retry` endpoint)

Jobs follow: `RUNNING → COMPLETED | COMPLETED_WITH_FAILURES`
- Jobs revert to RUNNING if a failed item is retried: `COMPLETED_WITH_FAILURES → RUNNING`

State transitions are enforced atomically via DynamoDB condition expressions.

### DynamoDB Single-Table Design

- Partition key: `PK` = `JOB#{jobId}`
- Sort key: `SK` = `META` (job metadata) or `ITEM#{itemId}` (item records)
- TTL attribute: `ttl` (Unix timestamp) - jobs auto-expire after configurable days (default 30)
- Table name configurable via `FORGELEDGER_TABLE` or `JOBS_TABLE` env vars, defaults to `ForgeLedger`

### Authentication

API key authentication via `X-API-KEY` header. Key is loaded from (in order):
1. appsettings: `ForgeLedger:ApiKey` (if set, used immediately for local dev)
2. AWS Secrets Manager: secret name from `FORGELEDGER_APIKEY_SECRET_NAME` env var (default: `ForgeLedger/API/Key`)

Excluded paths (no auth required): `/`, `/health`, `/swagger/*`

### API Endpoints

All item operations use colon-prefixed action syntax:
- `POST /jobs` - Create job (optional: `webhookUrl`, `ttlDays`)
- `GET /jobs/{jobId}` - Get job status
- `GET /jobs/{jobId}/items` - List items (query params: `status`, `limit`, `nextToken`)
- `POST /jobs/{jobId}/items:register` - Register items (idempotent)
- `POST /jobs/{jobId}/items/{itemId}:claim` - Claim item for processing
- `POST /jobs/{jobId}/items/{itemId}:complete` - Mark complete
- `POST /jobs/{jobId}/items/{itemId}:fail` - Mark failed
- `POST /jobs/{jobId}/items/{itemId}:retry` - Reset failed item to pending

### Error Handling

- `ArgumentException` → 400 Bad Request
- `InvalidOperationException` → 409 Conflict
- All errors use RFC7807 Problem Details format

### Webhooks

Jobs can specify a `webhookUrl` at creation. When the job completes (all items finished), ForgeLedger POSTs the `JobStatusResponse` to that URL. Webhook status is tracked in the job metadata (`webhookStatus`: SENT, FAILED:StatusCode, or FAILED:ExceptionType).

### TTL / Auto-Cleanup

Jobs and items automatically expire via DynamoDB TTL. Default is 30 days, configurable per-job via `ttlDays` in CreateJobRequest. Set to 0 to disable (not recommended).