using System.Threading.Tasks;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;

namespace ForgeLedger.Client
{
    public interface IForgeLedgerService
    {
        Task<CreateJobResponse> CreateJobAsync(CreateJobRequest request);
        Task<ClaimItemResponse> ClaimItemAsync(string jobId, string itemId);
        Task<JobStatusResponse> GetJobStatusAsync(string jobId);

        Task<RegisterItemsResponse> RegisterItemsAsync(string jobId, RegisterItemsRequest request);
        Task CompleteItemAsync(string jobId, string itemId, ItemCompleteRequest request);
        Task FailItemAsync(string jobId, string itemId, ItemFailRequest request);
    }
}