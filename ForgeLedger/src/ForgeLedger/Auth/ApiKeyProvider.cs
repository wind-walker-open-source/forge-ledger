using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace ForgeLedger.Auth;

public class ApiKeyProvider
{
    private const string DefaultSecretName = "ForgeLedger/API/Key";
    private const string SecretNameEnvVar = "FORGELEDGER_APIKEY_SECRET_NAME";
    private const string AppSettingsKey = "ForgeLedger:ApiKey";

    private readonly IAmazonSecretsManager? _secretsManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyProvider> _logger;
    private readonly string _secretName;

    private string? _cachedApiKey;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ApiKeyProvider(
        IConfiguration configuration,
        ILogger<ApiKeyProvider> logger,
        IAmazonSecretsManager? secretsManager = null)
    {
        _configuration = configuration;
        _logger = logger;
        _secretsManager = secretsManager;
        _secretName = Environment.GetEnvironmentVariable(SecretNameEnvVar)
            ?? DefaultSecretName;
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken ct = default)
    {
        // Check cache first
        if (_cachedApiKey != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedApiKey;
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cachedApiKey != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedApiKey;
            }

            // Try appsettings first (useful for local development)
            var apiKey = _configuration[AppSettingsKey];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _logger.LogDebug("API key loaded from appsettings");
            }
            else
            {
                // Fall back to Secrets Manager
                apiKey = await TryGetFromSecretsManagerAsync(ct);
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
            _logger.LogDebug("Secrets Manager client not available, skipping secret lookup");
            return null;
        }

        try
        {
            var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = _secretName
            }, ct);

            _logger.LogInformation("API key loaded from Secrets Manager at {SecretName}", _secretName);
            return response.SecretString;
        }
        catch (ResourceNotFoundException)
        {
            _logger.LogDebug("Secret {SecretName} not found in Secrets Manager", _secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve API key from Secrets Manager at {SecretName}", _secretName);
            return null;
        }
    }
}