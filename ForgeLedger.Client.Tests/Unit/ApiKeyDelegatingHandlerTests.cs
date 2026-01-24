using System.Net;
using AwesomeAssertions;
using ForgeLedger.Client.Tests.Helpers;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace ForgeLedger.Client.Tests.Unit;

public class ApiKeyDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_WhenApiKeyAvailable_AddsHeader()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "test-api-key"
        });
        var apiKeyProvider = new ApiKeyProvider(options);

        var innerHandler = new MockHttpMessageHandler();
        innerHandler.EnqueueResponse(HttpStatusCode.OK, "{}");

        var handler = new ApiKeyDelegatingHandler(apiKeyProvider)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await httpClient.SendAsync(request);

        // Assert
        innerHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = innerHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue();
        values.Should().Contain("test-api-key");
    }

    [Fact]
    public async Task SendAsync_WhenApiKeyNull_DoesNotAddHeader()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = null // No API key configured
        });
        var apiKeyProvider = new ApiKeyProvider(options, ssm: null); // No SSM client either

        var innerHandler = new MockHttpMessageHandler();
        innerHandler.EnqueueResponse(HttpStatusCode.OK, "{}");

        var handler = new ApiKeyDelegatingHandler(apiKeyProvider)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        await httpClient.SendAsync(request);

        // Assert
        innerHandler.SentRequests.Should().HaveCount(1);
        var sentRequest = innerHandler.SentRequests[0];
        sentRequest.Headers.Contains("X-API-KEY").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_RemovesExistingHeaderBeforeAdding()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "new-api-key"
        });
        var apiKeyProvider = new ApiKeyProvider(options);

        var innerHandler = new MockHttpMessageHandler();
        innerHandler.EnqueueResponse(HttpStatusCode.OK, "{}");

        var handler = new ApiKeyDelegatingHandler(apiKeyProvider)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");
        request.Headers.Add("X-API-KEY", "old-api-key");

        // Act
        await httpClient.SendAsync(request);

        // Assert
        var sentRequest = innerHandler.SentRequests[0];
        sentRequest.Headers.TryGetValues("X-API-KEY", out var values).Should().BeTrue();
        values.Should().HaveCount(1);
        values.Should().Contain("new-api-key");
        values.Should().NotContain("old-api-key");
    }

    [Fact]
    public async Task SendAsync_CallsInnerHandler()
    {
        // Arrange
        var options = Options.Create(new ForgeLedgerClientOptions
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "test-key"
        });
        var apiKeyProvider = new ApiKeyProvider(options);

        var innerHandler = new MockHttpMessageHandler();
        innerHandler.EnqueueResponse(HttpStatusCode.OK, "{\"result\":\"success\"}");

        var handler = new ApiKeyDelegatingHandler(apiKeyProvider)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/test");

        // Act
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        innerHandler.SentRequests.Should().HaveCount(1);
    }
}
