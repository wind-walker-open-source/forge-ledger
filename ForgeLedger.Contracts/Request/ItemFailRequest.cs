namespace ForgeLedger.Contracts.Request;

public sealed class ItemFailRequest
{
    public string Reason { get; set; } = null!;
    public string? Detail { get; set; }
}