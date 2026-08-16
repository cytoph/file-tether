using System.Net;

namespace FileTether.Core.Tests.Helpers;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _stubs = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void Respond(string urlSubstring, HttpStatusCode statusCode, string body)
    {
        _stubs[urlSubstring] = _ => new HttpResponseMessage(statusCode) { Content = new StringContent(body) };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        string url = request.RequestUri!.PathAndQuery;

        foreach (KeyValuePair<string, Func<HttpRequestMessage, HttpResponseMessage>> stub in _stubs)
        {
            if (url.Contains(stub.Key, StringComparison.Ordinal))
            {
                return Task.FromResult(stub.Value(request));
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No stub registered for {url}"),
        });
    }
}
