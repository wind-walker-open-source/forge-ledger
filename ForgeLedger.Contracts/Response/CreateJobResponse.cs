namespace ForgeLedger.Contracts.Response;

public sealed class CreateJobResponse
{
    public string JobId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string JobType { get; set; } = default!;
    public int ExpectedCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public string CreatedAt { get; set; } = default!;
    public string? WebhookUrl { get; set; }
}