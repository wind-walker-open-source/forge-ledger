using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ForgeLedger.Client;

/// <summary>
/// Provides API key resolution for the ForgeLedger client.
/// Checks options first (from appsettings), then falls back to AWS Secrets Manager.
/// </summary>
public class ApiKeyProvider
{
    private readonly ForgeLedgerClientOptions _options;
    private readonly IAmazonSecretsManager? _secretsManager;
    private readonly ILogger<ApiKeyProvider>? _logger;

    private string? _cachedApiKey;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public ApiKeyProvider(
        IOptions<ForgeLedgerClientOptions> options,
        IAmazonSecretsManager? secretsManager = null,
        ILogger<ApiKeyProvider>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _secretsManager = secretsManager;
        _logger = logger;
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken ct = default)
    {
        // Check cache first
        if (_cachedApiKey != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedApiKey;
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_cachedApiKey != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedApiKey;
            }

            // Try options first (from appsettings)
            var apiKey = _options.ApiKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                _logger?.LogDebug("API key loaded from options/appsettings");
            }
            else
            {
                // Fall back to Secrets Manager
                apiKey = await TryGetFromSecretsManagerAsync(ct).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                _cachedApiKey = apiKey;
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
            }

            return apiKey;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> TryGetFromSecretsManagerAsync(CancellationToken ct)
    {
        if (_secretsManager == null)
        {
            _logger?.LogDebug("Secrets Manager client not available, skipping secret lookup");
            return null;
        }

        var secretName = _options.ApiKeySecretName;
        if (string.IsNullOrEmpty(secretName))
        {
            _logger?.LogDebug("Secret name not configured");
            return null;
        }

        try
        {
            var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName
            }, ct).ConfigureAwait(false);

            _logger?.LogInformation("API key loaded from Secrets Manager at {SecretName}", secretName);
            return response.SecretString;
        }
        catch (ResourceNotFoundException)
        {
            _logger?.LogDebug("Secret {SecretName} not found in Secrets Manager", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to retrieve API key from Secrets Manager at {SecretName}", secretName);
            return null;
        }
    }
}