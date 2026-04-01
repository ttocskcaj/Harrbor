using System.Net;
using System.Text.Json;

namespace Harrbor.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests.AsReadOnly();

    public void QueueResponse(HttpStatusCode statusCode, object? content = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
        {
            var json = JsonSerializer.Serialize(content);
            response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }
        _responses.Enqueue(response);
    }

    public void QueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    public void QueueJsonResponse<T>(T content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(statusCode);
        var json = JsonSerializer.Serialize(content);
        response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _responses.Enqueue(response);
    }

    public void QueueErrorResponse(HttpStatusCode statusCode, string errorMessage)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(errorMessage, System.Text.Encoding.UTF8, "text/plain")
        };
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"No response queued for request: {request.Method} {request.RequestUri}");
        }

        return Task.FromResult(_responses.Dequeue());
    }

    public HttpClient CreateClient()
    {
        return new HttpClient(this)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    public void VerifyRequestCount(int expected)
    {
        if (_requests.Count != expected)
        {
            throw new InvalidOperationException(
                $"Expected {expected} requests but received {_requests.Count}");
        }
    }

    public void VerifyRequest(int index, HttpMethod method, string pathContains)
    {
        if (index >= _requests.Count)
        {
            throw new InvalidOperationException(
                $"Request index {index} out of range. Only {_requests.Count} requests were made.");
        }

        var request = _requests[index];
        if (request.Method != method)
        {
            throw new InvalidOperationException(
                $"Expected request {index} to be {method} but was {request.Method}");
        }

        if (!request.RequestUri!.PathAndQuery.Contains(pathContains))
        {
            throw new InvalidOperationException(
                $"Expected request {index} path to contain '{pathContains}' but was '{request.RequestUri.PathAndQuery}'");
        }
    }
}
