using System.Collections.Generic;

namespace ForgeLedger.Contracts.Request;

public sealed class CreateJobRequest
{
    public string JobType { get; set; } = default!;
    public int ExpectedCount { get; set; }
    public string? QueueName { get; set; }
    public string? QueueKind { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
