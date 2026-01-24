using System.Net;
using System.Text;

namespace ForgeLedger.Client.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<HttpRequestMessage> SentRequests { get; } = new();

    public void EnqueueResponse(HttpStatusCode statusCode, string content = "{}")
    {
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }

    public void EnqueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        SentRequests.Add(request);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No response configured for request: {request.Method} {request.RequestUri}");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
