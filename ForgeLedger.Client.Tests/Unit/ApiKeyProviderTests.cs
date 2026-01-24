using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ForgeLedger.Client.Tests.Unit;

public class ApiKeyProviderTests
{
    [Fact]
    public async Task GetApiKeyAsync_WhenApiKeyInOptions_ReturnsFromOptions()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "options-api-key"
        });
        var mockSsm = Substitute.For<IAmazonSimpleSystemsManagement>();
        var provider = new ApiKeyProvider(options, mockSsm);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().Be("options-api-key");
        // SSM should not be called when API key is in options
        await mockSsm.DidNotReceive().GetParameterAsync(
            Arg.Any<GetParameterRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenNoApiKeyInOptions_QueriesParameterStore()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null,
            ParameterStorePath = "/ForgeLedger/API/Key"
        });
        var mockSsm = Substitute.For<IAmazonSimpleSystemsManagement>();
        mockSsm.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Value = "ssm-api-key" }
            });

        var provider = new ApiKeyProvider(options, mockSsm);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().Be("ssm-api-key");
        await mockSsm.Received(1).GetParameterAsync(
            Arg.Is<GetParameterRequest>(r => r.Name == "/ForgeLedger/API/Key" && r.WithDecryption == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetApiKeyAsync_CachesResultFor5Minutes()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var mockSsm = Substitute.For<IAmazonSimpleSystemsManagement>();
        mockSsm.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse
            {
                Parameter = new Parameter { Value = "cached-key" }
            });

        var provider = new ApiKeyProvider(options, mockSsm);

        // Act
        var result1 = await provider.GetApiKeyAsync();
        var result2 = await provider.GetApiKeyAsync();
        var result3 = await provider.GetApiKeyAsync();

        // Assert
        result1.Should().Be("cached-key");
        result2.Should().Be("cached-key");
        result3.Should().Be("cached-key");
        // SSM should only be called once due to caching
        await mockSsm.Received(1).GetParameterAsync(
            Arg.Any<GetParameterRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSsmClientNull_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var provider = new ApiKeyProvider(options, ssm: null);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenParameterNotFound_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var mockSsm = Substitute.For<IAmazonSimpleSystemsManagement>();
        mockSsm.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ParameterNotFoundException("Parameter not found"));

        var mockLogger = Substitute.For<ILogger<ApiKeyProvider>>();
        var provider = new ApiKeyProvider(options, mockSsm, mockLogger);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSsmThrowsGenericException_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var mockSsm = Substitute.For<IAmazonSimpleSystemsManagement>();
        mockSsm.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("AWS error"));

        var mockLogger = Substitute.For<ILogger<ApiKeyProvider>>();
        var provider = new ApiKeyProvider(options, mockSsm, mockLogger);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_IsThreadSafe()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var callCount = 0;
        var mockSsm = Substitute.For<IAmazonSimpleSystemsManagement>();
        mockSsm.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref callCount);
                return new GetParameterResponse
                {
                    Parameter = new Parameter { Value = "concurrent-key" }
                };
            });

        var provider = new ApiKeyProvider(options, mockSsm);

        // Act - Call concurrently from multiple threads
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.GetApiKeyAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(r => r == "concurrent-key");
        // Due to caching and locking, SSM should only be called once
        callCount.Should().Be(1);
    }
}
