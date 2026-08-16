using System.Text.Json;
using FileTether.Core.Providers.Internal;
using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Providers;

public sealed class GitHubProvider : FileProvider<GitHubTetherEntry>
{
    public const string ProviderName = "github";

    private const string ApiName = "GitHub API";

    private readonly GitHubApiClient _apiClient;

    public override string Name { get; } = ProviderName;

    public GitHubProvider(GitHubApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    protected override async Task<Result<RemoteVersion>> GetLatestVersionAsync(GitHubTetherEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            string encodedPath = Uri.EscapeDataString(entry.SourcePath).Replace("%2F", "/");
            string url = $"repos/{entry.Repo}/commits?path={encodedPath}&per_page=1";

            using JsonDocument commits = await _apiClient.GetJsonAsync(ApiName, url, entry.Token, cancellationToken);

            if (commits.RootElement.GetArrayLength() == 0)
            {
                return Result.Fail<RemoteVersion>($"No commit history found for path '{entry.SourcePath}' in {entry.Repo}.");
            }

            string versionRef = JsonHelpers.GetRequiredStringProperty(commits.RootElement[0], "sha", ApiName);

            return new RemoteVersion { VersionRef = versionRef };
        }
        catch (ProviderResponseException exception)
        {
            return Result.Fail<RemoteVersion>(exception.Message);
        }
    }

    protected override async Task<Result<IReadOnlyList<RemoteFile>>> FetchAsync(GitHubTetherEntry entry, string versionRef, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<RemoteFile> files = await FetchFilesAsync(entry, versionRef, cancellationToken);

            return Result.Ok(files);
        }
        catch (ProviderResponseException exception)
        {
            return Result.Fail<IReadOnlyList<RemoteFile>>(exception.Message);
        }
    }

    private async Task<IReadOnlyList<RemoteFile>> FetchFilesAsync(GitHubTetherEntry entry, string versionRef, CancellationToken cancellationToken)
    {
        string url = $"repos/{entry.Repo}/contents/{entry.SourcePath}?ref={versionRef}";

        using JsonDocument contents = await _apiClient.GetJsonAsync(ApiName, url, entry.Token, cancellationToken);

        // object = single file with inline content, array = folder listing
        if (contents.RootElement.ValueKind == JsonValueKind.Object)
        {
            string encoding = contents.RootElement.TryGetProperty("encoding", out JsonElement encodingElement)
                ? encodingElement.GetString() ?? ""
                : "";

            // contents API omits inline content for 1-100MB files
            if (encoding != "base64")
            {
                string sha = JsonHelpers.GetRequiredStringProperty(contents.RootElement, "sha", ApiName);
                byte[] blobContent = await FetchBlobAsync(entry.Repo, sha, entry.Token, cancellationToken);

                return [new RemoteFile { RelativePath = "", Content = blobContent }];
            }

            string encoded = JsonHelpers.GetRequiredStringProperty(contents.RootElement, "content", ApiName);
            byte[] content = DecodeBase64Content(encoded);

            return [new RemoteFile { RelativePath = "", Content = content }];
        }

        return await FetchFolderAsync(entry, versionRef, cancellationToken);
    }

    private async Task<byte[]> FetchBlobAsync(string repo, string sha, string? token, CancellationToken cancellationToken)
    {
        string url = $"repos/{repo}/git/blobs/{sha}";

        using JsonDocument blob = await _apiClient.GetJsonAsync(ApiName, url, token, cancellationToken);

        string encoded = JsonHelpers.GetRequiredStringProperty(blob.RootElement, "content", ApiName);

        return DecodeBase64Content(encoded);
    }

    private async Task<IReadOnlyList<RemoteFile>> FetchFolderAsync(GitHubTetherEntry entry, string versionRef, CancellationToken cancellationToken)
    {
        string url = $"repos/{entry.Repo}/git/trees/{versionRef}?recursive=1";

        using JsonDocument tree = await _apiClient.GetJsonAsync(ApiName, url, entry.Token, cancellationToken);

        string prefix = $"{entry.SourcePath}/";
        List<RemoteFile> files = [];

        foreach (JsonElement item in JsonHelpers.GetRequiredProperty(tree.RootElement, "tree", ApiName).EnumerateArray())
        {
            if (JsonHelpers.GetRequiredStringProperty(item, "type", ApiName) != "blob")
            {
                continue;
            }

            string path = JsonHelpers.GetRequiredStringProperty(item, "path", ApiName);

            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string blobSha = JsonHelpers.GetRequiredStringProperty(item, "sha", ApiName);
            byte[] content = await FetchBlobAsync(entry.Repo, blobSha, entry.Token, cancellationToken);

            files.Add(new RemoteFile { RelativePath = path[prefix.Length..], Content = content });
        }

        if (files.Count == 0)
        {
            throw new ProviderResponseException($"No files found at '{entry.SourcePath}' in {entry.Repo} at {versionRef}.");
        }

        return files;
    }

    private static byte[] DecodeBase64Content(string encoded)
    {
        try
        {
            // GitHub MIME-wraps the base64 text with a \n every 60 chars, so stripping affects only the transport encoding, never the decoded file
            return Convert.FromBase64String(encoded.Replace("\n", ""));
        }
        catch (FormatException exception)
        {
            throw new ProviderResponseException($"GitHub API returned malformed base64 content: {exception.Message}", exception);
        }
    }
}
