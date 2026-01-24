using System.Collections.Generic;

namespace ForgeLedger.Contracts.Request;

public sealed class RegisterItemsRequest
{
    public List<string> ItemIds { get; set; } = new();
}