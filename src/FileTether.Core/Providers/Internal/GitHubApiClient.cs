using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FileTether.Core.Providers.Internal;

public sealed class GitHubApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubApiClient> _logger;

    public GitHubApiClient(HttpClient httpClient, ILogger<GitHubApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<JsonDocument> GetJsonAsync(string apiName, string relativeUrl, string? token, CancellationToken cancellationToken)
    {
        _logger.LogDebug("GET {RelativeUrl}", relativeUrl);

        using HttpRequestMessage request = CreateRequest(relativeUrl, token);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();

            JsonDocument? document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);

            if (document is null)
            {
                throw new ProviderResponseException($"{apiName} returned empty content for {relativeUrl}.");
            }

            return document;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderResponseException($"{apiName} request timed out or was canceled.");
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderResponseException(DescribeHttpFailure(apiName, relativeUrl, exception), exception);
        }
        catch (JsonException exception)
        {
            throw new ProviderResponseException($"{apiName} returned unexpected content: {exception.Message}", exception);
        }
    }

    public async Task<byte[]> GetBytesAsync(string apiName, string url, string? token, CancellationToken cancellationToken)
    {
        _logger.LogDebug("GET {Url}", url);

        using HttpRequestMessage request = CreateRequest(url, token);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderResponseException($"{apiName} request timed out or was canceled.");
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderResponseException(DescribeHttpFailure(apiName, url, exception), exception);
        }
    }

    private static HttpRequestMessage CreateRequest(string url, string? token)
    {
        HttpRequestMessage request = new(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static string DescribeHttpFailure(string apiName, string url, HttpRequestException exception)
    {
        return exception.StatusCode is { } statusCode
            ? $"{apiName} returned {(int)statusCode} for {url}."
            : $"{apiName} request failed: {exception.Message}";
    }
}
