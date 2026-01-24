namespace ForgeLedger.Contracts.Response;

public sealed class JobStatusResponse
{
    public string JobId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string JobType { get; set; } = null!;
    public int ExpectedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public string CreatedAt { get; set; } = null!;
    public string? CompletedAt { get; set; }
    public string? QueueName { get; set; }
    public string? QueueKind { get; set; }
    public string? WebhookUrl { get; set; }
    public string? WebhookStatus { get; set; }
}