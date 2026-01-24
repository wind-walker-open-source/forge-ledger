namespace ForgeLedger.Contracts.Response;

public sealed class RegisterItemsResponse
{
    public int Registered { get; set; }
    public int AlreadyExisted { get; set; }
}