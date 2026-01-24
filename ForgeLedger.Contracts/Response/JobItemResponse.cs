namespace ForgeLedger.Contracts.Response;

public sealed class JobItemResponse
{
    public string ItemId { get; set; } = default!;
    public string ItemStatus { get; set; } = default!;
    public int Attempts { get; set; }
    public string? ClaimedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? LastError { get; set; }
}