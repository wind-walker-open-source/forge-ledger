# ForgeLedger

A lightweight, deterministic job and workflow ledger for queue-driven and distributed systems.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> Part of the **Wind Walker Forge** ecosystem — tools designed for clarity, correctness, and operational integrity.

## Overview

ForgeLedger provides a simple HTTP API for registering jobs, tracking work items, and determining when all processing has completed. It's ideal for coordinating long-running batch operations such as SQS fan-out/fan-in workflows.

**Key Features:**
- Deterministic state machine for job/item tracking
- Webhook callbacks on job completion
- Automatic cleanup via DynamoDB TTL
- Retry failed items
- API key authentication
- .NET client SDK included

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- AWS account (for deployment)
- AWS CLI configured

### Build
```bash
dotnet build
```

### Run Locally
```bash
dotnet run --project ForgeLedger/src/ForgeLedger
```

Then open https://localhost:5001/swagger for API documentation.

### Run Tests
```bash
dotnet test
```

### Deploy to AWS
```bash
cd ForgeLedger/src/ForgeLedger
dotnet lambda deploy-serverless
```

## Project Structure

```
forge-ledger/
├── ForgeLedger/                    # Main API (AWS Lambda, .NET 8)
│   └── src/ForgeLedger/
├── ForgeLedger.Client/             # .NET Client SDK
├── ForgeLedger.Contracts/          # Shared DTOs
├── ForgeLedger.Tests/              # API/Service tests
└── ForgeLedger.Client.Tests/       # Client SDK tests
```

## Architecture

```
Client / Worker
    ↓ HTTP
API Gateway
    ↓
ForgeLedger (Lambda)
    ↓
DynamoDB
```

- **Runtime:** AWS Lambda (.NET 8, Minimal APIs)
- **API:** Amazon API Gateway (REST)
- **Storage:** DynamoDB (single-table design)

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/jobs` | Create a new job |
| GET | `/jobs/{jobId}` | Get job status |
| GET | `/jobs/{jobId}/items` | List items (with status filter) |
| POST | `/jobs/{jobId}/items:register` | Register items for a job |
| POST | `/jobs/{jobId}/items/{itemId}:claim` | Claim item for processing |
| POST | `/jobs/{jobId}/items/{itemId}:complete` | Mark item completed |
| POST | `/jobs/{jobId}/items/{itemId}:fail` | Mark item failed |
| POST | `/jobs/{jobId}/items/{itemId}:retry` | Retry a failed item |

## State Machine

**Items:** `PENDING → PROCESSING → COMPLETED | FAILED`
- Failed items can be retried: `FAILED → PENDING`

**Jobs:** `RUNNING → COMPLETED | COMPLETED_WITH_FAILURES`

## Client SDK Usage

```csharp
services.AddForgeLedgerClient(options =>
{
    options.BaseUrl = "https://your-api-url.com";
    options.ApiKey = "your-api-key";
});

// Inject and use
public class MyService(IForgeLedgerService forgeLedger)
{
    public async Task ProcessAsync()
    {
        var job = await forgeLedger.CreateJobAsync(new CreateJobRequest
        {
            JobType = "batch-processing",
            ExpectedCount = 100
        });

        // Register and process items...
    }
}
```

## Authentication

All endpoints (except `/`, `/health`, `/swagger/*`) require an API key via the `X-API-KEY` header.

Configure the API key in:
1. `appsettings.json`: `ForgeLedger:ApiKey`
2. AWS Parameter Store: `/ForgeLedger/API/Key` (fallback)

## Documentation

- [API Documentation](ForgeLedger/src/ForgeLedger/Readme.md) - Detailed API reference
- [CLAUDE.md](CLAUDE.md) - Development guide for AI assistants

## Use Cases

- Financial batch processing
- Payment orchestration
- Idempotent queue processing
- Distributed worker coordination
- Long-running fan-out workflows

## License

[MIT](LICENSE) - Copyright (c) 2026 Lister Potter / Wind Walker Forge, LLC

## Author

Built by **Lister Potter**
Wind Walker Forge, LLC
