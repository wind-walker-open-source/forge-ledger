using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;
using ForgeLedger.Tests.Helpers;
using NSubstitute;
using Xunit;

namespace ForgeLedger.Tests.Integration;

public class EndpointsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EndpointsIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-KEY", factory.TestApiKey);
    }

    #region Health & Root Endpoints

    [Fact]
    public async Task GetHealth_ReturnsOkWithStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ok");
    }

    [Fact]
    public async Task GetRoot_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ForgeLedger");
    }

    #endregion

    #region Jobs API

    [Fact]
    public async Task PostJobs_WithValidRequest_Returns201Created()
    {
        // Arrange
        var expectedResponse = new CreateJobResponse
        {
            JobId = "test-job-id",
            Status = "PENDING",
            JobType = "TestJob",
            ExpectedCount = 10
        };

        _factory.MockStore.CreateJobAsync(Arg.Any<CreateJobRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var request = new CreateJobRequest
        {
            JobType = "TestJob",
            ExpectedCount = 10
        };

        // Act
        var response = await _client.PostAsJsonAsync("/jobs", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateJobResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.JobId.Should().Be("test-job-id");
    }

    [Fact]
    public async Task PostJobs_WithMissingJobType_Returns400ValidationProblem()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            JobType = "",
            ExpectedCount = 10
        };

        // Act
        var response = await _client.PostAsJsonAsync("/jobs", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostJobs_WithZeroExpectedCount_Returns400ValidationProblem()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            JobType = "TestJob",
            ExpectedCount = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/jobs", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetJob_WhenNotFound_Returns404()
    {
        // Arrange
        _factory.MockStore.GetJobAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((JobStatusResponse?)null);

        // Act
        var response = await _client.GetAsync("/jobs/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetJob_WhenFound_Returns200WithJob()
    {
        // Arrange
        var expectedJob = new JobStatusResponse
        {
            JobId = "test-job",
            Status = "RUNNING",
            JobType = "TestJob",
            ExpectedCount = 10,
            CompletedCount = 5,
            FailedCount = 0
        };

        _factory.MockStore.GetJobAsync("test-job", Arg.Any<CancellationToken>())
            .Returns(expectedJob);

        // Act
        var response = await _client.GetAsync("/jobs/test-job");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JobStatusResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.JobId.Should().Be("test-job");
        result.Status.Should().Be("RUNNING");
    }

    #endregion

    #region Items API

    [Fact]
    public async Task PostRegisterItems_ReturnsRegistrationResult()
    {
        // Arrange
        var expectedResponse = new RegisterItemsResponse { Registered = 3, AlreadyExisted = 0 };
        _factory.MockStore.RegisterItemsAsync("test-job", Arg.Any<RegisterItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var request = new RegisterItemsRequest
        {
            ItemIds = new List<string> { "item1", "item2", "item3" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/jobs/test-job/items:register", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegisterItemsResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Registered.Should().Be(3);
    }

    [Fact]
    public async Task GetItems_WithStatusFilter_ReturnsFilteredItems()
    {
        // Arrange
        var expectedResponse = new GetItemsResponse
        {
            JobId = "test-job",
            Items = new List<JobItemResponse>
            {
                new() { ItemId = "item1", ItemStatus = "PENDING" },
                new() { ItemId = "item2", ItemStatus = "PENDING" }
            },
            TotalCount = 2
        };

        _factory.MockStore.GetItemsAsync("test-job", "PENDING", Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var response = await _client.GetAsync("/jobs/test-job/items?status=PENDING");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetItemsResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task PostClaimItem_WhenPending_Returns200()
    {
        // Arrange
        var expectedResponse = new ClaimItemResponse { ItemStatus = "PROCESSING", Attempts = 1 };
        _factory.MockStore.TryClaimItemAsync("test-job", "test-item", Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act - Note: endpoint uses colon prefix :claim
        var response = await _client.PostAsync("/jobs/test-job/items/test-item:claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ClaimItemResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.ItemStatus.Should().Be("PROCESSING");
    }

    [Fact]
    public async Task PostClaimItem_WhenAlreadyProcessing_Returns409Conflict()
    {
        // Arrange
        _factory.MockStore.TryClaimItemAsync("test-job", "test-item", Arg.Any<CancellationToken>())
            .Returns<ClaimItemResponse>(_ => throw new InvalidOperationException("Item is already PROCESSING"));

        // Act - Note: endpoint uses colon prefix :claim
        var response = await _client.PostAsync("/jobs/test-job/items/test-item:claim", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostRetryItem_WhenFailed_Returns200WithJobStatus()
    {
        // Arrange
        var expectedResponse = new JobStatusResponse
        {
            JobId = "test-job",
            Status = "RUNNING",
            JobType = "TestJob",
            ExpectedCount = 10,
            CompletedCount = 5,
            FailedCount = 0
        };

        _factory.MockStore.RetryItemAsync("test-job", "test-item", Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act - Note: endpoint uses colon prefix :retry
        var response = await _client.PostAsync("/jobs/test-job/items/test-item:retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task ProtectedEndpoint_WithoutApiKey_Returns401()
    {
        // Arrange - Create a client without API key
        var clientWithoutKey = _factory.CreateClient();

        // Act
        var response = await clientWithoutKey.GetAsync("/jobs/test-job");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoint_WithoutApiKey_Returns200()
    {
        // Arrange - Create a client without API key
        var clientWithoutKey = _factory.CreateClient();

        // Act
        var response = await clientWithoutKey.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
