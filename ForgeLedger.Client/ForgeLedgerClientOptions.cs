namespace ForgeLedger.Client;

public class ForgeLedgerClientOptions
{
    /// <summary>
    /// Base URL of the ForgeLedger API (e.g., "https://api.example.com").
    /// If null/empty, falls back to AWS Parameter Store at BaseUrlParameterPath.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// API key for authentication. If set, this is used directly.
    /// If null/empty, falls back to AWS Secrets Manager.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// AWS Secrets Manager secret name for the API key. Only used if ApiKey is not set.
    /// Defaults to "ForgeLedger/API/Key".
    /// </summary>
    public string ApiKeySecretName { get; set; } = "ForgeLedger/API/Key";

    /// <summary>
    /// AWS Parameter Store path for the Base URL. Only used if BaseUrl is not set.
    /// Defaults to "/ForgeLedger/API/BaseUrl".
    /// </summary>
    public string BaseUrlParameterPath { get; set; } = "/ForgeLedger/API/BaseUrl";
}