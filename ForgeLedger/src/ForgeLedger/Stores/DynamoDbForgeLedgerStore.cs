using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;
using ForgeLedger.Core;
using NUlid;

namespace ForgeLedger.Stores;

public class DynamoDbForgeLedgerStore : IForgeLedgerStore
{
    private const string MetaSk = "META";
    private readonly IAmazonDynamoDB _ddb;
    private readonly string _table;

    public DynamoDbForgeLedgerStore(IAmazonDynamoDB ddb, string tableName)
    {
        _ddb = ddb;
        _table = tableName;
    }

    public async Task<CreateJobResponse> CreateJobAsync(CreateJobRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.JobType))
            throw new ArgumentException("JobType is required.");
        if (req.ExpectedCount < 0)
            throw new ArgumentException("ExpectedCount must be >= 0.");

        var jobId = Ulid.NewUlid().ToString();
        var now = DateTimeOffset.UtcNow;

        var pk = Pk(jobId);

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue(pk),
            ["SK"] = new AttributeValue(MetaSk),
            ["jobId"] = new AttributeValue(jobId),
            ["jobType"] = new AttributeValue(req.JobType),
            ["status"] = new AttributeValue("RUNNING"),
            ["expectedCount"] = new AttributeValue { N = req.ExpectedCount.ToString(CultureInfo.InvariantCulture) },
            ["completedCount"] = new AttributeValue { N = "0" },
            ["failedCount"] = new AttributeValue { N = "0" },
            ["createdAt"] = new AttributeValue(now.ToString("O"))
        };

        Console.WriteLine($"Creating job {jobId} of type {req.JobType} expecting {req.ExpectedCount} items.");

        if (!string.IsNullOrWhiteSpace(req.QueueName))
            item["queueName"] = new AttributeValue(req.QueueName);
        if (!string.IsNullOrWhiteSpace(req.QueueKind))
            item["queueKind"] = new AttributeValue(req.QueueKind);

        if (req.Metadata is { Count: > 0 })
        {
            item["metadata"] = new AttributeValue
            {
                M = req.Metadata.ToDictionary(kvp => kvp.Key, kvp => new AttributeValue(kvp.Value))
            };
        }

        // Ensure job header is only created once per id (ULID is unique, but keep it safe).
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
        }, ct);

        return new CreateJobResponse
        {
            JobId = jobId,
            Status = "PENDING",
            JobType = req.JobType,
            ExpectedCount = req.ExpectedCount,
            CompletedCount = 0,
            FailedCount = 0,
            CreatedAt = now.ToString("O")
        };
    }

    public async Task<RegisterItemsResponse> RegisterItemsAsync(string jobId, RegisterItemsRequest req,
        CancellationToken ct)
    {
        if (req.ItemIds is null || req.ItemIds.Count == 0)
            return new RegisterItemsResponse
            {
                Registered = 0,
                AlreadyExisted = 0
            };

        var registered = 0;
        var existed = 0;

        foreach (var raw in req.ItemIds)
        {
            var itemId = (raw ?? string.Empty).Trim();
            if (itemId.Length == 0) continue;

            var put = new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue(Pk(jobId)),
                    ["SK"] = new AttributeValue(ItemSk(itemId)),
                    ["jobId"] = new AttributeValue(jobId),
                    ["itemId"] = new AttributeValue(itemId),
                    ["itemStatus"] = new AttributeValue("PENDING"),
                    ["attempts"] = new AttributeValue { N = "0" },
                    ["updatedAt"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O"))
                },
                ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
            };

            try
            {
                await _ddb.PutItemAsync(put, ct);
                registered++;
            }
            catch (ConditionalCheckFailedException)
            {
                existed++;
            }
        }

        return new RegisterItemsResponse
            {
                Registered = registered, 
                AlreadyExisted = existed
            };
    }

    public async Task<ClaimItemResponse> TryClaimItemAsync(string jobId, string itemId, CancellationToken ct)
    {
        // First read item to short-circuit duplicates cleanly.
        var current = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(Pk(jobId)),
                ["SK"] = new AttributeValue(ItemSk(itemId))
            },
            ConsistentRead = true
        }, ct);

        if (current.Item is null || current.Item.Count == 0)
        {
            // Item wasn't registered; create it lazily to be tolerant.
            await RegisterItemsAsync(jobId, new RegisterItemsRequest{ItemIds = [itemId] }, ct);
            current = await _ddb.GetItemAsync(new GetItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue(Pk(jobId)),
                    ["SK"] = new AttributeValue(ItemSk(itemId))
                },
                ConsistentRead = true
            }, ct);
        }

        var status = current.Item.TryGetValue("itemStatus", out var s) ? s.S : "PENDING";
        var attempts = current.Item.TryGetValue("attempts", out var a) && int.TryParse(a.N, out var n) ? n : 0;

        if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot claim an item that is already COMPLETED.");
        }

        if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot claim an item that is already FAILED.");
        }

        if (string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot claim an item that is already PROCESSING.");
        }

        // Attempt to claim by transitioning to PROCESSING and incrementing attempts.
        try
        {
            var update = await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue(Pk(jobId)),
                    ["SK"] = new AttributeValue(ItemSk(itemId))
                },
                // Only claim if the item is PENDING (or missing status, treated as PENDING)
                ConditionExpression = "attribute_not_exists(itemStatus) OR itemStatus = :pending",
                UpdateExpression = "SET itemStatus = :processing, claimedAt = :now, updatedAt = :now ADD attempts :one",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pending"] = new AttributeValue("PENDING"),
                    [":processing"] = new AttributeValue("PROCESSING"),
                    [":now"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O")),
                    [":one"] = new AttributeValue { N = "1" }
                },
                ReturnValues = ReturnValue.ALL_NEW
            }, ct);

            var newStatus = update.Attributes.TryGetValue("itemStatus", out var ns) ? ns.S : "PROCESSING";
            var newAttempts = update.Attributes.TryGetValue("attempts", out var na) && int.TryParse(na.N, out var nn) ? nn : attempts + 1;
            return new ClaimItemResponse
                {
                    AlreadyCompleted = false, 
                    ItemStatus = newStatus, 
                    Attempts = newAttempts
                };
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException("Cannot claim item because it is no longer PENDING.");
        }
    }

    public async Task<JobStatusResponse?> GetJobAsync(string jobId, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(Pk(jobId)),
                ["SK"] = new AttributeValue(MetaSk)
            },
            ConsistentRead = true
        }, ct);

        if (resp.Item is null || resp.Item.Count == 0)
            return null;

        return ToJobStatus(resp.Item);
    }

    public async Task<JobStatusResponse> MarkItemCompletedAsync(string jobId, string itemId, ItemCompleteRequest req, CancellationToken ct)
    {
        // Enforce state machine: PENDING -> PROCESSING -> COMPLETED
        var current = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(Pk(jobId)),
                ["SK"] = new AttributeValue(ItemSk(itemId))
            },
            ConsistentRead = true
        }, ct);

        if (current.Item is null || current.Item.Count == 0)
        {
            throw new InvalidOperationException($"Item '{itemId}' was not registered for job '{jobId}'.");
        }

        var status = current.Item.TryGetValue("itemStatus", out var s) ? s.S : "PENDING";

        if (!string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot complete an item that has not been claimed. Claim it first.");
            }

            if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot complete an item that is already COMPLETED.");
            }

            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot complete an item that is already FAILED.");
            }

            throw new InvalidOperationException($"Cannot complete item in state '{status}'.");
        }

        // Transition item to COMPLETED exactly once.
        try
        {
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue(Pk(jobId)),
                    ["SK"] = new AttributeValue(ItemSk(itemId))
                },
                ConditionExpression = "itemStatus = :processing",
                UpdateExpression = "SET itemStatus = :completed, updatedAt = :now REMOVE lastError",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":processing"] = new AttributeValue("PROCESSING"),
                    [":completed"] = new AttributeValue("COMPLETED"),
                    [":now"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O"))
                }
            }, ct);

            // Increment job completedCount exactly once per successful item completion transition.
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue(Pk(jobId)),
                    ["SK"] = new AttributeValue(MetaSk)
                },
                UpdateExpression = "ADD completedCount :one SET updatedAt = :now",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":one"] = new AttributeValue { N = "1" },
                    [":now"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O"))
                }
            }, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException("Cannot complete item because it is no longer PROCESSING.");
        }

        // Try to complete the job if counts match.
        await TryMarkJobCompletedAsync(jobId, ct);

        var job = await GetJobAsync(jobId, ct);
        return job ?? throw new InvalidOperationException($"Job '{jobId}' not found.");
    }

    public async Task<JobStatusResponse> MarkItemFailedAsync(string jobId, string itemId, ItemFailRequest req, CancellationToken ct)
    {
        // Enforce state machine: PENDING -> PROCESSING -> FAILED
        var current = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(Pk(jobId)),
                ["SK"] = new AttributeValue(ItemSk(itemId))
            },
            ConsistentRead = true
        }, ct);

        if (current.Item is null || current.Item.Count == 0)
        {
            throw new InvalidOperationException($"Item '{itemId}' was not registered for job '{jobId}'.");
        }

        var status = current.Item.TryGetValue("itemStatus", out var s) ? s.S : "PENDING";

        if (!string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot fail an item that has not been claimed. Claim it first.");
            }

            if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot fail an item that is already COMPLETED.");
            }

            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Cannot fail an item that is already FAILED.");
            }

            throw new InvalidOperationException($"Cannot fail item in state '{status}'.");
        }

        await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(Pk(jobId)),
                ["SK"] = new AttributeValue(ItemSk(itemId))
            },
            ConditionExpression = "itemStatus = :processing",
            UpdateExpression = "SET itemStatus = :failed, updatedAt = :now, lastError = :err",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":processing"] = new AttributeValue("PROCESSING"),
                [":failed"] = new AttributeValue("FAILED"),
                [":now"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O")),
                [":err"] = new AttributeValue($"{req.Reason}{(string.IsNullOrWhiteSpace(req.Detail) ? string.Empty : " | " + req.Detail)}")
            }
        }, ct);

        await _ddb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue(Pk(jobId)),
                ["SK"] = new AttributeValue(MetaSk)
            },
            UpdateExpression = "ADD failedCount :one SET updatedAt = :now",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":one"] = new AttributeValue { N = "1" },
                [":now"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O"))
            }
        }, ct);

        await TryMarkJobCompletedAsync(jobId, ct);

        var job = await GetJobAsync(jobId, ct);
        return job ?? throw new InvalidOperationException($"Job '{jobId}' not found.");
    }

    private async Task TryMarkJobCompletedAsync(string jobId, CancellationToken ct)
    {
        // Read current counts and set status if complete.
        var job = await GetJobAsync(jobId, ct);
        if (job is null) return;

        var done = (job.CompletedCount + job.FailedCount) >= job.ExpectedCount;
        if (!done) return;

        if (string.Equals(job.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Status, "COMPLETED_WITH_FAILURES", StringComparison.OrdinalIgnoreCase))
            return;

        var newStatus = job.FailedCount > 0 ? "COMPLETED_WITH_FAILURES" : "COMPLETED";

        try
        {
            await _ddb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _table,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue(Pk(jobId)),
                    ["SK"] = new AttributeValue(MetaSk)
                },
                // Only transition from RUNNING -> COMPLETED* once.
                ConditionExpression = "status = :running",
                UpdateExpression = "SET status = :s, completedAt = :now, updatedAt = :now",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":running"] = new AttributeValue("RUNNING"),
                    [":s"] = new AttributeValue(newStatus),
                    [":now"] = new AttributeValue(DateTimeOffset.UtcNow.ToString("O"))
                }
            }, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            // Already transitioned by another worker.
        }
    }

    private static string Pk(string jobId) => $"JOB#{jobId}";
    private static string ItemSk(string itemId) => $"ITEM#{itemId}";

    private static JobStatusResponse ToJobStatus(Dictionary<string, AttributeValue> item)
    {
        string S(string k, string d = "") => item.TryGetValue(k, out var v) ? v.S : d;
        int N(string k) => item.TryGetValue(k, out var v) && int.TryParse(v.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        return new JobStatusResponse{
            JobId = S("jobId"),
            Status = S("status"),
            JobType = S("jobType"),
            ExpectedCount = N("expectedCount"),
            CompletedCount = N("completedCount"),
            FailedCount = N("failedCount"),
            CreatedAt = S("createdAt"),
            CompletedAt = item.TryGetValue("completedAt", out var ca) ? ca.S : null,
            QueueName = item.TryGetValue("queueName", out var qn) ? qn.S : null,
            QueueKind = item.TryGetValue("queueKind", out var qk) ? qk.S : null
        };
    }   
}