using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using ForgeLedger.Client.Tests.Helpers;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Xunit;

namespace ForgeLedger.Client.Tests.Integration;

/// <summary>
/// Tests that verify the DI wiring correctly invokes the ApiKeyDelegatingHandler
/// for ALL client methods, not just CreateJobAsync.
/// </summary>
public class ServiceCollectionIntegrationTests
{
    private const string BaseUrl = "https://api.example.com";
    private const string TestApiKey = "test-api-key-12345";

    /// <summary>
    /// Creates a service provider with the ForgeLedger client configured
    /// and a mock HTTP handler to capture requests.
    /// </summary>
    private (IServiceProvider, MockHttpMessageHandler) CreateTestServices()
    {
        var services = new ServiceCollection();
        var mockHandler = new MockHttpMessageHandler();

        // Add the ForgeLedger client with API key configured
        services.AddForgeLedgerClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.ApiKey = TestApiKey;
        });

        // Replace the HttpClient's primary handler with our mock
        // This requires configuring the HttpClient with our handler
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                // Insert our mock as the primary handler (innermost)
                builder.PrimaryHandler = mockHandler;
            });
        });

        var provider = services.BuildServiceProvider();
        return (provider, mockHandler);
    }

    [Fact]
    public async Task CreateJobAsync_ViaServiceCollection_IncludesApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        var response = new CreateJobResponse
        {
            JobId = "job-123",
            JobType = "TestJob",
            ExpectedCount = 10,
            Status = "PENDING"
        };
        mockHandler.EnqueueResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await client.CreateJobAsync(new CreateJobRequest { JobType = "TestJob", ExpectedCount = 10 });

        // Assert
        mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = mockHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue("API key header should be present");
        values.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task ClaimItemAsync_ViaServiceCollection_IncludesApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        var response = new ClaimItemResponse { ItemStatus = "PROCESSING", Attempts = 1 };
        mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await client.ClaimItemAsync("job-123", "item-456");

        // Assert
        mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = mockHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue("API key header should be present for ClaimItemAsync");
        values.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task CompleteItemAsync_ViaServiceCollection_IncludesApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        mockHandler.EnqueueResponse(HttpStatusCode.NoContent);

        // Act
        await client.CompleteItemAsync("job-123", "item-456", new ItemCompleteRequest());

        // Assert
        mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = mockHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue("API key header should be present for CompleteItemAsync");
        values.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task FailItemAsync_ViaServiceCollection_IncludesApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        mockHandler.EnqueueResponse(HttpStatusCode.NoContent);

        // Act
        await client.FailItemAsync("job-123", "item-456", new ItemFailRequest { Reason = "Test failure" });

        // Assert
        mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = mockHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue("API key header should be present for FailItemAsync");
        values.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task RegisterItemsAsync_ViaServiceCollection_IncludesApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        var response = new RegisterItemsResponse { Registered = 2, AlreadyExisted = 0 };
        mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await client.RegisterItemsAsync("job-123", new RegisterItemsRequest { ItemIds = new List<string> { "item1", "item2" } });

        // Assert
        mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = mockHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue("API key header should be present for RegisterItemsAsync");
        values.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task GetJobStatusAsync_ViaServiceCollection_IncludesApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        var response = new JobStatusResponse
        {
            JobId = "job-123",
            Status = "RUNNING",
            JobType = "TestJob",
            ExpectedCount = 10
        };
        mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await client.GetJobStatusAsync("job-123");

        // Assert
        mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = mockHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue("API key header should be present for GetJobStatusAsync");
        values.Should().Contain(TestApiKey);
    }

    [Fact]
    public async Task MultipleSequentialCalls_ViaServiceCollection_AllIncludeApiKeyHeader()
    {
        // Arrange
        var (provider, mockHandler) = CreateTestServices();
        var client = provider.GetRequiredService<IForgeLedgerService>();

        // Queue responses for: CreateJob, ClaimItem, CompleteItem
        mockHandler.EnqueueResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new CreateJobResponse { JobId = "job-123", JobType = "Test", ExpectedCount = 1, Status = "PENDING" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new ClaimItemResponse { ItemStatus = "PROCESSING", Attempts = 1 },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        mockHandler.EnqueueResponse(HttpStatusCode.NoContent);

        // Act
        await client.CreateJobAsync(new CreateJobRequest { JobType = "Test", ExpectedCount = 1 });
        await client.ClaimItemAsync("job-123", "item-456");
        await client.CompleteItemAsync("job-123", "item-456", new ItemCompleteRequest());

        // Assert - ALL requests should have the API key header
        mockHandler.SentRequests.Should().HaveCount(3);

        foreach (var (request, index) in mockHandler.SentRequests.Select((r, i) => (r, i)))
        {
            request.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue(
                $"API key header should be present for request #{index + 1}: {request.Method} {request.RequestUri}");
            values.Should().Contain(TestApiKey);
        }
    }
}