using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace ForgeLedger.Auth;

public class ApiKeyProvider
{
    private const string ParameterStorePath = "/ForgeLedger/API/Key";
    private const string AppSettingsKey = "ForgeLedger:ApiKey";

    private readonly IAmazonSimpleSystemsManagement? _ssm;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyProvider> _logger;

    private string? _cachedApiKey;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ApiKeyProvider(
        IConfiguration configuration,
        ILogger<ApiKeyProvider> logger,
        IAmazonSimpleSystemsManagement? ssm = null)
    {
        _configuration = configuration;
        _logger = logger;
        _ssm = ssm;
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

            // Try appsettings first
            var apiKey = _configuration[AppSettingsKey];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _logger.LogDebug("API key loaded from appsettings");
            }
            else
            {
                // Fall back to Parameter Store
                apiKey = await TryGetFromParameterStoreAsync(ct);
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
            _logger.LogDebug("SSM client not available, skipping Parameter Store lookup");
            return null;
        }

        try
        {
            var response = await _ssm.GetParameterAsync(new GetParameterRequest
            {
                Name = ParameterStorePath,
                WithDecryption = true
            }, ct);

            _logger.LogInformation("API key loaded from Parameter Store at {Path}", ParameterStorePath);
            return response.Parameter?.Value;
        }
        catch (ParameterNotFoundException)
        {
            _logger.LogDebug("Parameter {Path} not found in Parameter Store", ParameterStorePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve API key from Parameter Store at {Path}", ParameterStorePath);
            return null;
        }
    }
}