namespace ForgeLedger.Client;

public class ForgeLedgerClientOptions
{
    /// <summary>
    /// Base URL of the ForgeLedger API (e.g., "https://api.example.com").
    /// </summary>
    public string BaseUrl { get; set; } = null!;

    /// <summary>
    /// API key for authentication. If set, this is used directly.
    /// If null/empty, falls back to AWS Parameter Store.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// AWS Parameter Store path for the API key. Only used if ApiKey is not set.
    /// Defaults to "/ForgeLedger/API/Key".
    /// </summary>
    public string ParameterStorePath { get; set; } = "/ForgeLedger/API/Key";
}