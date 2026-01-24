# ForgeLedger.Contracts

Shared request/response DTOs for the ForgeLedger API.

## Overview

This package contains the data transfer objects (DTOs) used by both the ForgeLedger API and the ForgeLedger.Client SDK. If you're building a client that communicates with ForgeLedger, you typically want the [ForgeLedger.Client](https://www.nuget.org/packages/ForgeLedger.Client) package instead, which includes these contracts.

## Installation

```bash
dotnet add package ForgeLedger.Contracts
```

## Included Types

### Request DTOs
- `CreateJobRequest` - Create a new job
- `RegisterItemsRequest` - Register items for processing
- `CompleteItemRequest` - Mark item as completed
- `FailItemRequest` - Mark item as failed

### Response DTOs
- `CreateJobResponse` - Job creation result
- `JobStatusResponse` - Full job status with counts
- `RegisterItemsResponse` - Registration result
- `ClaimItemResponse` - Claim result with attempt count
- `GetItemsResponse` - Paginated items list
- `JobItemResponse` - Individual item status

## Links

- [GitHub Repository](https://github.com/wind-walker-open-source/forge-ledger)
- [ForgeLedger.Client Package](https://www.nuget.org/packages/ForgeLedger.Client)
- [Documentation](https://github.com/wind-walker-open-source/forge-ledger#readme)

## License

[MIT](https://github.com/wind-walker-open-source/forge-ledger/blob/main/LICENSE)
