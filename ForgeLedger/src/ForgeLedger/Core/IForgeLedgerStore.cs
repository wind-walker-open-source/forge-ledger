using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;

namespace ForgeLedger.Core;

public interface IForgeLedgerStore
{
    Task<CreateJobResponse> CreateJobAsync(CreateJobRequest req, CancellationToken ct);
    Task<RegisterItemsResponse> RegisterItemsAsync(string jobId, RegisterItemsRequest req, CancellationToken ct);
    Task<ClaimItemResponse> TryClaimItemAsync(string jobId, string itemId, CancellationToken ct);
    Task<JobStatusResponse?> GetJobAsync(string jobId, CancellationToken ct);
    Task<GetItemsResponse> GetItemsAsync(string jobId, string? status, int? limit, string? nextToken, CancellationToken ct);
    Task<JobStatusResponse> MarkItemCompletedAsync(string jobId, string itemId, ItemCompleteRequest req, CancellationToken ct);
    Task<JobStatusResponse> MarkItemFailedAsync(string jobId, string itemId, ItemFailRequest req, CancellationToken ct);
    Task<JobStatusResponse> RetryItemAsync(string jobId, string itemId, CancellationToken ct);
}