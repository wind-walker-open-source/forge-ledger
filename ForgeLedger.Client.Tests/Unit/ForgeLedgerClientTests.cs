using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using ForgeLedger.Client.Tests.Helpers;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;
using Microsoft.Extensions.Options;
using Xunit;

namespace ForgeLedger.Client.Tests.Unit;

public class ForgeLedgerClientTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly ForgeLedgerClient _client;
    private const string BaseUrl = "https://api.example.com";

    public ForgeLedgerClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_mockHandler);
        var options = Options.Create(new ForgeLedgerClientOptions { BaseUrl = BaseUrl });
        _client = new ForgeLedgerClient(httpClient, options);
    }

    #region CreateJobAsync Tests

    [Fact]
    public async Task CreateJobAsync_SendsPostToJobsEndpoint()
    {
        // Arrange
        var request = new CreateJobRequest { JobType = "TestJob", ExpectedCount = 10 };
        var response = new CreateJobResponse
        {
            JobId = "test-id",
            JobType = "TestJob",
            ExpectedCount = 10,
            Status = "PENDING"
        };
        _mockHandler.EnqueueResponse(HttpStatusCode.Created, JsonSerializer.Serialize(response));

        // Act
        await _client.CreateJobAsync(request);

        // Assert
        _mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _mockHandler.SentRequests[0];
        sentRequest.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/jobs");
    }

    [Fact]
    public async Task CreateJobAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _client.CreateJobAsync(null!));
    }

    [Fact]
    public async Task CreateJobAsync_DeserializesResponse()
    {
        // Arrange
        var request = new CreateJobRequest { JobType = "TestJob", ExpectedCount = 10 };
        var expectedResponse = new CreateJobResponse
        {
            JobId = "test-job-123",
            JobType = "TestJob",
            ExpectedCount = 10,
            Status = "PENDING",
            CompletedCount = 0,
            FailedCount = 0
        };
        _mockHandler.EnqueueResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(expectedResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        var result = await _client.CreateJobAsync(request);

        // Assert
        result.JobId.Should().Be("test-job-123");
        result.JobType.Should().Be("TestJob");
        result.ExpectedCount.Should().Be(10);
    }

    [Fact]
    public async Task CreateJobAsync_WhenServerReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        var request = new CreateJobRequest { JobType = "TestJob", ExpectedCount = 10 };
        _mockHandler.EnqueueResponse(HttpStatusCode.InternalServerError, "{\"error\":\"Server error\"}");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.CreateJobAsync(request));

        ex.Message.Should().Contain("500");
    }

    #endregion

    #region ClaimItemAsync Tests

    [Fact]
    public async Task ClaimItemAsync_SendsPostToClaimEndpoint()
    {
        // Arrange
        var response = new ClaimItemResponse { ItemStatus = "PROCESSING", Attempts = 1 };
        _mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await _client.ClaimItemAsync("job-123", "item-456");

        // Assert
        _mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _mockHandler.SentRequests[0];
        sentRequest.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/jobs/job-123/items/item-456/claim");
    }

    [Fact]
    public async Task ClaimItemAsync_WithNullJobId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.ClaimItemAsync(null!, "item-123"));
    }

    [Fact]
    public async Task ClaimItemAsync_WithNullItemId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.ClaimItemAsync("job-123", null!));
    }

    [Fact]
    public async Task ClaimItemAsync_UrlEncodesParameters()
    {
        // Arrange
        var response = new ClaimItemResponse { ItemStatus = "PROCESSING", Attempts = 1 };
        _mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await _client.ClaimItemAsync("job/with/slashes", "item-123");

        // Assert
        var sentRequest = _mockHandler.SentRequests[0];
        // Slashes should be URL-encoded
        sentRequest.RequestUri!.ToString().Should().Contain("job%2Fwith%2Fslashes");
        sentRequest.RequestUri.ToString().Should().Contain("item-123");
    }

    #endregion

    #region GetJobStatusAsync Tests

    [Fact]
    public async Task GetJobStatusAsync_SendsGetToJobEndpoint()
    {
        // Arrange
        var response = new JobStatusResponse
        {
            JobId = "job-123",
            Status = "RUNNING",
            JobType = "TestJob",
            ExpectedCount = 10
        };
        _mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await _client.GetJobStatusAsync("job-123");

        // Assert
        _mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _mockHandler.SentRequests[0];
        sentRequest.Method.Should().Be(HttpMethod.Get);
        sentRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/jobs/job-123");
    }

    [Fact]
    public async Task GetJobStatusAsync_WithNullJobId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.GetJobStatusAsync(null!));
    }

    #endregion

    #region RegisterItemsAsync Tests

    [Fact]
    public async Task RegisterItemsAsync_SendsPostToRegisterEndpoint()
    {
        // Arrange
        var request = new RegisterItemsRequest { ItemIds = new List<string> { "item1", "item2" } };
        var response = new RegisterItemsResponse { Registered = 2, AlreadyExisted = 0 };
        _mockHandler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Act
        await _client.RegisterItemsAsync("job-123", request);

        // Assert
        _mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _mockHandler.SentRequests[0];
        sentRequest.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/jobs/job-123/items:register");
    }

    [Fact]
    public async Task RegisterItemsAsync_WithNullJobId_ThrowsArgumentException()
    {
        // Arrange
        var request = new RegisterItemsRequest { ItemIds = new List<string> { "item1" } };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.RegisterItemsAsync(null!, request));
    }

    [Fact]
    public async Task RegisterItemsAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _client.RegisterItemsAsync("job-123", null!));
    }

    #endregion

    #region CompleteItemAsync Tests

    [Fact]
    public async Task CompleteItemAsync_SendsPostToCompleteEndpoint()
    {
        // Arrange
        var request = new ItemCompleteRequest();
        _mockHandler.EnqueueResponse(HttpStatusCode.NoContent);

        // Act
        await _client.CompleteItemAsync("job-123", "item-456", request);

        // Assert
        _mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _mockHandler.SentRequests[0];
        sentRequest.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/jobs/job-123/items/item-456/complete");
    }

    [Fact]
    public async Task CompleteItemAsync_WithNullJobId_ThrowsArgumentException()
    {
        // Arrange
        var request = new ItemCompleteRequest();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.CompleteItemAsync(null!, "item-123", request));
    }

    [Fact]
    public async Task CompleteItemAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _client.CompleteItemAsync("job-123", "item-456", null!));
    }

    #endregion

    #region FailItemAsync Tests

    [Fact]
    public async Task FailItemAsync_SendsPostToFailEndpoint()
    {
        // Arrange
        var request = new ItemFailRequest { Reason = "Test failure" };
        _mockHandler.EnqueueResponse(HttpStatusCode.NoContent);

        // Act
        await _client.FailItemAsync("job-123", "item-456", request);

        // Assert
        _mockHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = _mockHandler.SentRequests[0];
        sentRequest.Method.Should().Be(HttpMethod.Post);
        sentRequest.RequestUri!.ToString().Should().Be($"{BaseUrl}/jobs/job-123/items/item-456/fail");
    }

    [Fact]
    public async Task FailItemAsync_WithNullJobId_ThrowsArgumentException()
    {
        // Arrange
        var request = new ItemFailRequest { Reason = "Test" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _client.FailItemAsync(null!, "item-123", request));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task WhenServerReturns4xx_ThrowsHttpRequestExceptionWithBody()
    {
        // Arrange
        var request = new CreateJobRequest { JobType = "TestJob", ExpectedCount = 10 };
        _mockHandler.EnqueueResponse(HttpStatusCode.BadRequest, "{\"error\":\"Invalid request\"}");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.CreateJobAsync(request));

        ex.Message.Should().Contain("400");
        ex.Message.Should().Contain("Invalid request");
    }

    [Fact]
    public async Task WhenServerReturns5xx_ThrowsHttpRequestExceptionWithBody()
    {
        // Arrange
        var request = new CreateJobRequest { JobType = "TestJob", ExpectedCount = 10 };
        _mockHandler.EnqueueResponse(HttpStatusCode.ServiceUnavailable, "{\"error\":\"Service unavailable\"}");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.CreateJobAsync(request));

        ex.Message.Should().Contain("503");
    }

    #endregion

    #region BaseUrl Configuration Tests

    [Fact]
    public async Task GetBaseUri_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var httpClient = new HttpClient(_mockHandler);
        var options = Options.Create(new ForgeLedgerClientOptions { BaseUrl = null });
        var client = new ForgeLedgerClient(httpClient, options);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateJobAsync(new CreateJobRequest { JobType = "Test", ExpectedCount = 1 }));
    }

    #endregion
}
