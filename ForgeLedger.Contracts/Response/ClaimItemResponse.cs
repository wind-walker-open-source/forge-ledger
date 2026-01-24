namespace ForgeLedger.Contracts.Response;

public sealed class ClaimItemResponse
{
    public bool AlreadyCompleted { get; set; }
    public string ItemStatus { get; set; } = default!;
    public int Attempts { get; set; }
}