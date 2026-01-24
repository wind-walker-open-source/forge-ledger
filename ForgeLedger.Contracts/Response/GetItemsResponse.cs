using System.Collections.Generic;

namespace ForgeLedger.Contracts.Response;

public sealed class GetItemsResponse
{
    public string JobId { get; set; } = default!;
    public List<JobItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public string? NextToken { get; set; }
}