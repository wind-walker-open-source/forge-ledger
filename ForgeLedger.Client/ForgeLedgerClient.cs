using System.Net.Http;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;

namespace ForgeLedger.Client
{
    public class ForgeLedgerClient : IForgeLedgerService
    {
        private readonly HttpClient _http;
        private readonly ForgeLedgerClientOptions _options;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ForgeLedgerClient(HttpClient http, ForgeLedgerClientOptions options)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        private Uri GetBaseUri()
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                throw new InvalidOperationException("ForgeLedgerClientOptions.BaseUrl must be configured.");
            }

            return new Uri(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        }

        private static string Esc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null/empty.", nameof(value));
            }

            return Uri.EscapeDataString(value);
        }

        private async Task<string> SendAsync(HttpRequestMessage httpRequest)
        {
            // Best practice: accept JSON responses explicitly
            httpRequest.Headers.Accept.Clear();
            httpRequest.Headers.Accept.ParseAdd("application/json");

            using var response = await _http.SendAsync(httpRequest).ConfigureAwait(false);

            var body = response.Content == null
                ? string.Empty
                : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return string.Empty;
            }

            if (!response.IsSuccessStatusCode)
            {
                var snippet = body;
                if (!string.IsNullOrEmpty(snippet) && snippet.Length > 2048)
                {
                    snippet = snippet.Substring(0, 2048) + "…";
                }

                throw new HttpRequestException(
                    $"ForgeLedger request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {snippet}");
            }

            return body ?? string.Empty;
        }

        private async Task<T> SendJsonAsync<T>(HttpMethod method, Uri endpoint, object payloadOrNull)
        {
            using var httpRequest = new HttpRequestMessage(method, endpoint);

            if (payloadOrNull != null)
            {
                var json = JsonSerializer.Serialize(payloadOrNull, JsonOptions);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var body = await SendAsync(httpRequest).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new HttpRequestException($"ForgeLedger returned an empty response body for {method} {endpoint}.");
            }

            var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
            if (model == null)
            {
                throw new HttpRequestException($"ForgeLedger returned an unreadable response body for {method} {endpoint}.");
            }

            return model;
        }

        private async Task SendNoContentAsync(HttpMethod method, Uri endpoint, object payloadOrNull)
        {
            using var httpRequest = new HttpRequestMessage(method, endpoint);

            if (payloadOrNull != null)
            {
                var json = JsonSerializer.Serialize(payloadOrNull, JsonOptions);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            _ = await SendAsync(httpRequest).ConfigureAwait(false);
        }

        public async Task<CreateJobResponse> CreateJobAsync(CreateJobRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var baseUri = GetBaseUri();
            var endpoint = new Uri(baseUri, "jobs");

            return await SendJsonAsync<CreateJobResponse>(HttpMethod.Post, endpoint, request).ConfigureAwait(false);
        }

        public async Task<ClaimItemResponse> ClaimItemAsync(string jobId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("jobId is required.", nameof(jobId));
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("itemId is required.", nameof(itemId));
            }

            var baseUri = GetBaseUri();
            var endpoint = new Uri(baseUri, $"jobs/{Esc(jobId)}/items/{Esc(itemId)}/claim");

            return await SendJsonAsync<ClaimItemResponse>(HttpMethod.Post, endpoint, payloadOrNull: null).ConfigureAwait(false);
        }

        public async Task<JobStatusResponse> GetJobStatusAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("jobId is required.", nameof(jobId));
            }

            var baseUri = GetBaseUri();
            var endpoint = new Uri(baseUri, $"jobs/{Esc(jobId)}");

            return await SendJsonAsync<JobStatusResponse>(HttpMethod.Get, endpoint, payloadOrNull: null).ConfigureAwait(false);
        }

        public async Task<RegisterItemsResponse> RegisterItemsAsync(string jobId, RegisterItemsRequest request)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("jobId is required.", nameof(jobId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var baseUri = GetBaseUri();
            // Note: this route uses ':' which is valid in the URL path
            var endpoint = new Uri(baseUri, $"jobs/{Esc(jobId)}/items:register");

            return await SendJsonAsync<RegisterItemsResponse>(HttpMethod.Post, endpoint, request).ConfigureAwait(false);
        }

        public async Task CompleteItemAsync(string jobId, string itemId, ItemCompleteRequest request)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("jobId is required.", nameof(jobId));
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("itemId is required.", nameof(itemId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var baseUri = GetBaseUri();
            var endpoint = new Uri(baseUri, $"jobs/{Esc(jobId)}/items/{Esc(itemId)}/complete");

            await SendNoContentAsync(HttpMethod.Post, endpoint, request).ConfigureAwait(false);
        }

        public async Task FailItemAsync(string jobId, string itemId, ItemFailRequest request)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("jobId is required.", nameof(jobId));
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("itemId is required.", nameof(itemId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var baseUri = GetBaseUri();
            var endpoint = new Uri(baseUri, $"jobs/{Esc(jobId)}/items/{Esc(itemId)}/fail");

            await SendNoContentAsync(HttpMethod.Post, endpoint, request).ConfigureAwait(false);
        }
    }
}