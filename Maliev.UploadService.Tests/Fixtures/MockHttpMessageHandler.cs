using Maliev.Aspire.ServiceDefaults.IAM;
using System.Net;

namespace Maliev.UploadService.Tests.Fixtures;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _requests = new();

    public IReadOnlyList<HttpRequestMessage> Requests => _requests.AsReadOnly();

    public void QueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    public void QueueResponse(HttpStatusCode statusCode, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
        {
            response.Content = new StringContent(content);
        }
        _responses.Enqueue(response);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (_responses.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("No mock response configured")
            };
        }

        var response = _responses.Dequeue();
        await Task.Delay(10, cancellationToken); // Simulate network delay
        return response;
    }
}


