using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
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
        var mockSecretsManager = Substitute.For<IAmazonSecretsManager>();
        var provider = new ApiKeyProvider(options, mockSecretsManager);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().Be("options-api-key");
        // Secrets Manager should not be called when API key is in options
        await mockSecretsManager.DidNotReceive().GetSecretValueAsync(
            Arg.Any<GetSecretValueRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenNoApiKeyInOptions_QueriesSecretsManager()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null,
            ApiKeySecretName = "ForgeLedger/API/Key"
        });
        var mockSecretsManager = Substitute.For<IAmazonSecretsManager>();
        mockSecretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                SecretString = "secrets-manager-api-key"
            });

        var provider = new ApiKeyProvider(options, mockSecretsManager);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().Be("secrets-manager-api-key");
        await mockSecretsManager.Received(1).GetSecretValueAsync(
            Arg.Is<GetSecretValueRequest>(r => r.SecretId == "ForgeLedger/API/Key"),
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
        var mockSecretsManager = Substitute.For<IAmazonSecretsManager>();
        mockSecretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                SecretString = "cached-key"
            });

        var provider = new ApiKeyProvider(options, mockSecretsManager);

        // Act
        var result1 = await provider.GetApiKeyAsync();
        var result2 = await provider.GetApiKeyAsync();
        var result3 = await provider.GetApiKeyAsync();

        // Assert
        result1.Should().Be("cached-key");
        result2.Should().Be("cached-key");
        result3.Should().Be("cached-key");
        // Secrets Manager should only be called once due to caching
        await mockSecretsManager.Received(1).GetSecretValueAsync(
            Arg.Any<GetSecretValueRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSecretsManagerClientNull_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var provider = new ApiKeyProvider(options, secretsManager: null);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSecretNotFound_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var mockSecretsManager = Substitute.For<IAmazonSecretsManager>();
        mockSecretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ResourceNotFoundException("Secret not found"));

        var mockLogger = Substitute.For<ILogger<ApiKeyProvider>>();
        var provider = new ApiKeyProvider(options, mockSecretsManager, mockLogger);

        // Act
        var result = await provider.GetApiKeyAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WhenSecretsManagerThrowsGenericException_ReturnsNull()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null
        });
        var mockSecretsManager = Substitute.For<IAmazonSecretsManager>();
        mockSecretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("AWS error"));

        var mockLogger = Substitute.For<ILogger<ApiKeyProvider>>();
        var provider = new ApiKeyProvider(options, mockSecretsManager, mockLogger);

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
        var mockSecretsManager = Substitute.For<IAmazonSecretsManager>();
        mockSecretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref callCount);
                return new GetSecretValueResponse
                {
                    SecretString = "concurrent-key"
                };
            });

        var provider = new ApiKeyProvider(options, mockSecretsManager);

        // Act - Call concurrently from multiple threads
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.GetApiKeyAsync())
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(r => r == "concurrent-key");
        // Due to caching and locking, Secrets Manager should only be called once
        callCount.Should().Be(1);
    }
}