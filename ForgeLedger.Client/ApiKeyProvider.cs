using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ForgeLedger.Client;

/// <summary>
/// Provides API key resolution for the ForgeLedger client.
/// Checks options first (from appsettings), then falls back to AWS Parameter Store.
/// </summary>
public class ApiKeyProvider
{
    private readonly ForgeLedgerClientOptions _options;
    private readonly IAmazonSimpleSystemsManagement? _ssm;
    private readonly ILogger<ApiKeyProvider>? _logger;

    private string? _cachedApiKey;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public ApiKeyProvider(
        IOptions<ForgeLedgerClientOptions> options,
        IAmazonSimpleSystemsManagement? ssm = null,
        ILogger<ApiKeyProvider>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _ssm = ssm;
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
                // Fall back to Parameter Store
                apiKey = await TryGetFromParameterStoreAsync(ct).ConfigureAwait(false);
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

    private async Task<string?> TryGetFromParameterStoreAsync(CancellationToken ct)
    {
        if (_ssm == null)
        {
            _logger?.LogDebug("SSM client not available, skipping Parameter Store lookup");
            return null;
        }

        var parameterPath = _options.ParameterStorePath;
        if (string.IsNullOrEmpty(parameterPath))
        {
            _logger?.LogDebug("Parameter Store path not configured");
            return null;
        }

        try
        {
            var response = await _ssm.GetParameterAsync(new GetParameterRequest
            {
                Name = parameterPath,
                WithDecryption = true
            }, ct).ConfigureAwait(false);

            _logger?.LogInformation("API key loaded from Parameter Store at {Path}", parameterPath);
            return response.Parameter?.Value;
        }
        catch (ParameterNotFoundException)
        {
            _logger?.LogDebug("Parameter {Path} not found in Parameter Store", parameterPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to retrieve API key from Parameter Store at {Path}", parameterPath);
            return null;
        }
    }
}