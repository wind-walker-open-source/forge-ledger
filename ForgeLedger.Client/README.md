# ForgeLedger.Client

.NET client SDK for [ForgeLedger](https://github.com/wind-walker-open-source/forge-ledger) - a lightweight, deterministic job and workflow ledger for queue-driven and distributed systems.

## Installation

```bash
dotnet add package ForgeLedger.Client
```

## Quick Start

### 1. Register Services

```csharp
services.AddForgeLedgerClient(options =>
{
    options.BaseUrl = "https://your-api-url.com";
    options.ApiKey = "your-api-key";
});
```

### 2. Inject and Use

```csharp
public class BatchProcessor(IForgeLedgerService forgeLedger)
{
    public async Task ProcessBatchAsync(List<string> itemIds)
    {
        // Create a job
        var job = await forgeLedger.CreateJobAsync(new CreateJobRequest
        {
            JobType = "batch-processing",
            ExpectedCount = itemIds.Count,
            WebhookUrl = "https://your-app.com/webhook"
        });

        // Register items
        await forgeLedger.RegisterItemsAsync(job.JobId, new RegisterItemsRequest
        {
            ItemIds = itemIds
        });

        // Process items (typically done by workers)
        foreach (var itemId in itemIds)
        {
            var claim = await forgeLedger.ClaimItemAsync(job.JobId, itemId);
            if (claim != null)
            {
                try
                {
                    // Do work...
                    await forgeLedger.CompleteItemAsync(job.JobId, itemId);
                }
                catch
                {
                    await forgeLedger.FailItemAsync(job.JobId, itemId, new FailItemRequest
                    {
                        Reason = "Processing failed"
                    });
                }
            }
        }

        // Check job status
        var status = await forgeLedger.GetJobAsync(job.JobId);
        Console.WriteLine($"Job {status.Status}: {status.CompletedCount}/{status.ExpectedCount}");
    }
}
```

## API Key Configuration

The client supports multiple API key sources:

1. **Direct configuration** (shown above)
2. **AWS Parameter Store** - Set `UseAwsParameterStore = true` and optionally customize `AwsParameterName`

```csharp
services.AddForgeLedgerClient(options =>
{
    options.BaseUrl = "https://your-api-url.com";
    options.UseAwsParameterStore = true;
    options.AwsParameterName = "/MyApp/ForgeLedger/ApiKey"; // optional
});
```

## Available Methods

| Method | Description |
|--------|-------------|
| `CreateJobAsync` | Create a new job |
| `GetJobAsync` | Get job status and counts |
| `RegisterItemsAsync` | Register items for a job |
| `GetItemsAsync` | List items with optional status filter |
| `ClaimItemAsync` | Claim an item for processing |
| `CompleteItemAsync` | Mark item as completed |
| `FailItemAsync` | Mark item as failed |
| `RetryItemAsync` | Retry a failed item |

## Item State Machine

```
PENDING -> PROCESSING -> COMPLETED
                     \-> FAILED -> PENDING (via retry)
```

## Links

- [GitHub Repository](https://github.com/wind-walker-open-source/forge-ledger)
- [API Documentation](https://github.com/wind-walker-open-source/forge-ledger/blob/main/ForgeLedger/src/ForgeLedger/Readme.md)

## License

[MIT](https://github.com/wind-walker-open-source/forge-ledger/blob/main/LICENSE)
