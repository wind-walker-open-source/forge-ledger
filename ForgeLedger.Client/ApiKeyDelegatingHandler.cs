using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ForgeLedger.Client;

/// <summary>
/// HTTP message handler that adds the X-API-KEY header to all requests.
/// </summary>
public class ApiKeyDelegatingHandler : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-API-KEY";

    private readonly ApiKeyProvider _apiKeyProvider;

    public ApiKeyDelegatingHandler(ApiKeyProvider apiKeyProvider)
    {
        _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
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
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}