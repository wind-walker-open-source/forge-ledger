using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Stores;
using ForgeLedger.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ForgeLedger.Tests.Unit.Stores;

public class DynamoDbForgeLedgerStoreTests
{
    private readonly IAmazonDynamoDB _mockDdb;
    private readonly IHttpClientFactory _mockHttpClientFactory;
    private readonly ILogger<DynamoDbForgeLedgerStore> _mockLogger;
    private readonly DynamoDbForgeLedgerStore _store;
    private const string TableName = "TestTable";

    public DynamoDbForgeLedgerStoreTests()
    {
        _mockDdb = Substitute.For<IAmazonDynamoDB>();
        _mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        _mockLogger = Substitute.For<ILogger<DynamoDbForgeLedgerStore>>();
        _store = new DynamoDbForgeLedgerStore(_mockDdb, TableName, _mockHttpClientFactory, _mockLogger);
    }

    #region CreateJobAsync Tests

    [Fact]
    public async Task CreateJobAsync_WithValidRequest_ReturnsNewJob()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            JobType = "TestJob",
            ExpectedCount = 10
        };

        _mockDdb.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        var result = await _store.CreateJobAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().NotBeNullOrEmpty();
        result.JobType.Should().Be("TestJob");
        result.ExpectedCount.Should().Be(10);
        result.CompletedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateJobAsync_WithMissingJobType_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            JobType = "",
            ExpectedCount = 10
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.CreateJobAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateJobAsync_WithNegativeExpectedCount_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            JobType = "TestJob",
            ExpectedCount = -1
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.CreateJobAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateJobAsync_WithWebhookUrl_StoresWebhookUrl()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            JobType = "TestJob",
            ExpectedCount = 10,
            WebhookUrl = "https://example.com/webhook"
        };

        PutItemRequest? capturedRequest = null;
        _mockDdb.PutItemAsync(Arg.Do<PutItemRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        var result = await _store.CreateJobAsync(request, CancellationToken.None);

        // Assert
        result.WebhookUrl.Should().Be("https://example.com/webhook");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Item.Should().ContainKey("webhookUrl");
        capturedRequest.Item["webhookUrl"].S.Should().Be("https://example.com/webhook");
    }

    #endregion

    #region TryClaimItemAsync Tests - State Machine

    [Fact]
    public async Task TryClaimItemAsync_WhenItemIsPending_TransitionsToProcessing()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PENDING")
            });

        _mockDdb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse
            {
                Attributes = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PROCESSING", attempts: 1)
            });

        // Act
        var result = await _store.TryClaimItemAsync(jobId, itemId, CancellationToken.None);

        // Assert
        result.ItemStatus.Should().Be("PROCESSING");
        result.Attempts.Should().Be(1);
        result.AlreadyCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimItemAsync_WhenItemIsProcessing_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PROCESSING")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.TryClaimItemAsync(jobId, itemId, CancellationToken.None));

        ex.Message.Should().Contain("PROCESSING");
    }

    [Fact]
    public async Task TryClaimItemAsync_WhenItemIsCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "COMPLETED")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.TryClaimItemAsync(jobId, itemId, CancellationToken.None));

        ex.Message.Should().Contain("COMPLETED");
    }

    [Fact]
    public async Task TryClaimItemAsync_WhenItemIsFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "FAILED")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.TryClaimItemAsync(jobId, itemId, CancellationToken.None));

        ex.Message.Should().Contain("FAILED");
    }

    #endregion

    #region MarkItemCompletedAsync Tests - State Machine

    [Fact]
    public async Task MarkItemCompletedAsync_WhenItemIsProcessing_TransitionsToCompleted()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";
        var request = new ItemCompleteRequest();

        // Item is PROCESSING
        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new GetItemResponse { Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PROCESSING") },
                new GetItemResponse { Item = DynamoDbTestHelpers.CreateJobMetaItem(jobId, completedCount: 1) }
            );

        _mockDdb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        // Act
        var result = await _store.MarkItemCompletedAsync(jobId, itemId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _mockDdb.Received(2).UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkItemCompletedAsync_WhenItemIsPending_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";
        var request = new ItemCompleteRequest();

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PENDING")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.MarkItemCompletedAsync(jobId, itemId, request, CancellationToken.None));

        ex.Message.Should().Contain("Claim it first");
    }

    [Fact]
    public async Task MarkItemCompletedAsync_WhenItemIsAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";
        var request = new ItemCompleteRequest();

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "COMPLETED")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.MarkItemCompletedAsync(jobId, itemId, request, CancellationToken.None));

        ex.Message.Should().Contain("already COMPLETED");
    }

    #endregion

    #region MarkItemFailedAsync Tests - State Machine

    [Fact]
    public async Task MarkItemFailedAsync_WhenItemIsProcessing_TransitionsToFailed()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";
        var request = new ItemFailRequest { Reason = "Test failure", Detail = "Details" };

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new GetItemResponse { Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PROCESSING") },
                new GetItemResponse { Item = DynamoDbTestHelpers.CreateJobMetaItem(jobId, failedCount: 1) }
            );

        _mockDdb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        // Act
        var result = await _store.MarkItemFailedAsync(jobId, itemId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await _mockDdb.Received(2).UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkItemFailedAsync_WhenItemIsPending_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";
        var request = new ItemFailRequest { Reason = "Test" };

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PENDING")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.MarkItemFailedAsync(jobId, itemId, request, CancellationToken.None));

        ex.Message.Should().Contain("Claim it first");
    }

    #endregion

    #region RetryItemAsync Tests

    [Fact]
    public async Task RetryItemAsync_WhenItemIsFailed_TransitionsToPending()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new GetItemResponse { Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "FAILED") },
                new GetItemResponse { Item = DynamoDbTestHelpers.CreateJobMetaItem(jobId, failedCount: 0) }
            );

        _mockDdb.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        // Act
        var result = await _store.RetryItemAsync(jobId, itemId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        // Verify UpdateItemAsync was called at least twice (item status + decrement failedCount)
        await _mockDdb.ReceivedWithAnyArgs(3).UpdateItemAsync(default!, default);
    }

    [Fact]
    public async Task RetryItemAsync_WhenItemIsNotFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "PENDING")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.RetryItemAsync(jobId, itemId, CancellationToken.None));

        ex.Message.Should().Contain("Only FAILED items can be retried");
    }

    [Fact]
    public async Task RetryItemAsync_WhenItemIsCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobId = "test-job";
        var itemId = "test-item";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateItemRecord(jobId, itemId, "COMPLETED")
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.RetryItemAsync(jobId, itemId, CancellationToken.None));

        ex.Message.Should().Contain("Only FAILED items can be retried");
    }

    #endregion

    #region GetJobAsync Tests

    [Fact]
    public async Task GetJobAsync_WhenJobExists_ReturnsJob()
    {
        // Arrange
        var jobId = "test-job";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateJobMetaItem(jobId)
            });

        // Act
        var result = await _store.GetJobAsync(jobId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.JobId.Should().Be(jobId);
        result.Status.Should().Be("RUNNING");
    }

    [Fact]
    public async Task GetJobAsync_WhenJobNotFound_ReturnsNull()
    {
        // Arrange
        var jobId = "nonexistent-job";

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = null });

        // Act
        var result = await _store.GetJobAsync(jobId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region RegisterItemsAsync Tests

    [Fact]
    public async Task RegisterItemsAsync_WithNewItems_ReturnsCorrectRegisteredCount()
    {
        // Arrange
        var jobId = "test-job";
        var request = new RegisterItemsRequest
        {
            ItemIds = new List<string> { "item1", "item2", "item3" }
        };

        // Return job meta for TTL lookup
        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateJobMetaItem(jobId)
            });

        _mockDdb.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        var result = await _store.RegisterItemsAsync(jobId, request, CancellationToken.None);

        // Assert
        result.Registered.Should().Be(3);
        result.AlreadyExisted.Should().Be(0);
    }

    [Fact]
    public async Task RegisterItemsAsync_WithExistingItems_ReturnsCorrectAlreadyExistedCount()
    {
        // Arrange
        var jobId = "test-job";
        var request = new RegisterItemsRequest
        {
            ItemIds = new List<string> { "item1", "item2" }
        };

        _mockDdb.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                Item = DynamoDbTestHelpers.CreateJobMetaItem(jobId)
            });

        // First item succeeds, second throws ConditionalCheckFailedException
        _mockDdb.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => new PutItemResponse(),
                _ => throw new ConditionalCheckFailedException("Item already exists")
            );

        // Act
        var result = await _store.RegisterItemsAsync(jobId, request, CancellationToken.None);

        // Assert
        result.Registered.Should().Be(1);
        result.AlreadyExisted.Should().Be(1);
    }

    [Fact]
    public async Task RegisterItemsAsync_WithEmptyList_ReturnsZeroCounts()
    {
        // Arrange
        var jobId = "test-job";
        var request = new RegisterItemsRequest
        {
            ItemIds = new List<string>()
        };

        // Act
        var result = await _store.RegisterItemsAsync(jobId, request, CancellationToken.None);

        // Assert
        result.Registered.Should().Be(0);
        result.AlreadyExisted.Should().Be(0);
    }

    #endregion

    #region GetItemsAsync Tests

    [Fact]
    public async Task GetItemsAsync_WithStatusFilter_ReturnsFilteredItems()
    {
        // Arrange
        var jobId = "test-job";

        _mockDdb.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    DynamoDbTestHelpers.CreateItemRecord(jobId, "item1", "PENDING"),
                    DynamoDbTestHelpers.CreateItemRecord(jobId, "item2", "PENDING")
                }
            });

        // Act
        var result = await _store.GetItemsAsync(jobId, "PENDING", null, null, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.ItemStatus == "PENDING");
    }

    [Fact]
    public async Task GetItemsAsync_WithPagination_ReturnsNextToken()
    {
        // Arrange
        var jobId = "test-job";

        _mockDdb.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = new List<Dictionary<string, AttributeValue>>
                {
                    DynamoDbTestHelpers.CreateItemRecord(jobId, "item1", "PENDING")
                },
                LastEvaluatedKey = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new($"JOB#{jobId}"),
                    ["SK"] = new("ITEM#item1")
                }
            });

        // Act
        var result = await _store.GetItemsAsync(jobId, null, 1, null, CancellationToken.None);

        // Assert
        result.NextToken.Should().NotBeNullOrEmpty();
    }

    #endregion
}
