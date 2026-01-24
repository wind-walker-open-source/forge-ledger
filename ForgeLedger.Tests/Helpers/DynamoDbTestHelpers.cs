using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace ForgeLedger.Tests.Helpers;

public static class DynamoDbTestHelpers
{
    public static Dictionary<string, AttributeValue> CreateJobMetaItem(
        string jobId,
        string status = "RUNNING",
        string jobType = "TestJob",
        int expectedCount = 10,
        int completedCount = 0,
        int failedCount = 0,
        string? webhookUrl = null,
        string? webhookStatus = null)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"JOB#{jobId}"),
            ["SK"] = new("META"),
            ["jobId"] = new(jobId),
            ["status"] = new(status),
            ["jobType"] = new(jobType),
            ["expectedCount"] = new() { N = expectedCount.ToString(CultureInfo.InvariantCulture) },
            ["completedCount"] = new() { N = completedCount.ToString(CultureInfo.InvariantCulture) },
            ["failedCount"] = new() { N = failedCount.ToString(CultureInfo.InvariantCulture) },
            ["createdAt"] = new(DateTimeOffset.UtcNow.ToString("O"))
        };

        if (webhookUrl != null)
            item["webhookUrl"] = new(webhookUrl);

        if (webhookStatus != null)
            item["webhookStatus"] = new(webhookStatus);

        return item;
    }

    public static Dictionary<string, AttributeValue> CreateItemRecord(
        string jobId,
        string itemId,
        string status = "PENDING",
        int attempts = 0,
        string? lastError = null)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new($"JOB#{jobId}"),
            ["SK"] = new($"ITEM#{itemId}"),
            ["jobId"] = new(jobId),
            ["itemId"] = new(itemId),
            ["itemStatus"] = new(status),
            ["attempts"] = new() { N = attempts.ToString(CultureInfo.InvariantCulture) },
            ["updatedAt"] = new(DateTimeOffset.UtcNow.ToString("O"))
        };

        if (lastError != null)
            item["lastError"] = new(lastError);

        return item;
    }

    public static string Pk(string jobId) => $"JOB#{jobId}";
    public static string ItemSk(string itemId) => $"ITEM#{itemId}";
    public const string MetaSk = "META";
}
