using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ForgeLedger.Client;

/// <summary>
/// HTTP message handler that adds the X-API-KEY header to all requests.
/// </summary>
public class ApiKeyDelegatingHandler : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-API-KEY";

    private readonly ApiKeyProvider _apiKeyProvider;
    private readonly ILogger<ApiKeyDelegatingHandler>? _logger;

    public ApiKeyDelegatingHandler(ApiKeyProvider apiKeyProvider, ILogger<ApiKeyDelegatingHandler>? logger = null)
    {
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = await _apiKeyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Remove(ApiKeyHeaderName);
            request.Headers.Add(ApiKeyHeaderName, apiKey);
            _logger?.LogDebug("Added X-API-KEY header to request: {Method} {Uri}", request.Method, request.RequestUri);
        }
        else
        {
            _logger?.LogWarning("No API key available for request: {Method} {Uri}. Request will be sent without authentication.", request.Method, request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}