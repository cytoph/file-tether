using System.Text;
using System.Text.Json;
using FileTether.Core.Providers.Internal;
using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Providers;

public sealed class GistProvider : FileProvider<GistTetherEntry>
{
    public const string ProviderName = "gist";

    private const string ApiName = "Gist API";

    private readonly GitHubApiClient _apiClient;

    public override string Name { get; } = ProviderName;

    public GistProvider(GitHubApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    protected override async Task<Result<RemoteVersion>> GetLatestVersionAsync(GistTetherEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"gists/{entry.GistId}";

            using JsonDocument gist = await _apiClient.GetJsonAsync(ApiName, url, entry.Token, cancellationToken);
            JsonElement history = JsonHelpers.GetRequiredProperty(gist.RootElement, "history", ApiName);

            if (history.GetArrayLength() == 0)
            {
                return Result.Fail<RemoteVersion>($"Gist {entry.GistId} has no history.");
            }

            string versionRef = JsonHelpers.GetRequiredStringProperty(history[0], "version", ApiName);

            return new RemoteVersion { VersionRef = versionRef };
        }
        catch (ProviderResponseException exception)
        {
            return Result.Fail<RemoteVersion>(exception.Message);
        }
    }

    protected override async Task<Result<IReadOnlyList<RemoteFile>>> FetchAsync(GistTetherEntry entry, string versionRef, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"gists/{entry.GistId}/{versionRef}";

            using JsonDocument gist = await _apiClient.GetJsonAsync(ApiName, url, entry.Token, cancellationToken);
            JsonElement files = JsonHelpers.GetRequiredProperty(gist.RootElement, "files", ApiName);
            JsonElement file = SelectFile(files, entry);

            byte[] content;

            if (file.TryGetProperty("truncated", out JsonElement truncated) && truncated.GetBoolean())
            {
                content = await GetBytesAsync(file, entry.Token, cancellationToken);
            }
            else
            {
                string rawContent = JsonHelpers.GetRequiredStringProperty(file, "content", ApiName);
                content = Encoding.UTF8.GetBytes(rawContent);
            }

            List<RemoteFile> resultFiles = [new RemoteFile { RelativePath = "", Content = content }];

            return Result.Ok<IReadOnlyList<RemoteFile>>(resultFiles);
        }
        catch (ProviderResponseException exception)
        {
            return Result.Fail<IReadOnlyList<RemoteFile>>(exception.Message);
        }
    }

    private Task<byte[]> GetBytesAsync(JsonElement file, string? token, CancellationToken cancellationToken)
    {
        string rawUrl = JsonHelpers.GetRequiredStringProperty(file, "raw_url", ApiName);

        return _apiClient.GetBytesAsync(ApiName, rawUrl, token, cancellationToken);
    }

    private static JsonElement SelectFile(JsonElement files, GistTetherEntry entry)
    {
        List<string> filenames = [.. files.EnumerateObject().Select(property => property.Name)];

        if (string.IsNullOrEmpty(entry.Filename))
        {
            if (filenames.Count != 1)
            {
                throw new ProviderResponseException($"Gist {entry.GistId} has multiple files ({string.Join(", ", filenames)}) - specify 'filename' in the config.");
            }

            return files.GetProperty(filenames[0]);
        }

        if (!files.TryGetProperty(entry.Filename, out JsonElement selected))
        {
            throw new ProviderResponseException($"Gist {entry.GistId} has no file '{entry.Filename}'. Available: {string.Join(", ", filenames)}.");
        }

        return selected;
    }
}
