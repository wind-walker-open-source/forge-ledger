using System.Collections.Generic;

namespace ForgeLedger.Contracts.Request;

public sealed class CreateJobRequest
{
    public string JobType { get; set; } = default!;
    public int ExpectedCount { get; set; }
    public string? QueueName { get; set; }
    public string? QueueKind { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Number of days until the job and its items are automatically deleted.
    /// Defaults to 30 days. Set to 0 to disable TTL (not recommended).
    /// </summary>
    public int? TtlDays { get; set; }

    /// <summary>
    /// Optional URL to receive a POST callback when the job completes.
    /// The callback payload will be the JobStatusResponse.
    /// </summary>
    public string? WebhookUrl { get; set; }
}
