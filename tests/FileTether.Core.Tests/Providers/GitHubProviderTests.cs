using System.Net;
using System.Text;
using FileTether.Core;
using FileTether.Core.Providers;
using FileTether.Core.Providers.Internal;
using FileTether.Core.Providers.Models;
using FileTether.Core.Tests.Helpers;
using FileTether.Core.Tethering.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.Providers;

public sealed class GitHubProviderTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly GitHubProvider _provider;

    public GitHubProviderTests()
    {
        _provider = new GitHubProvider(CreateApiClient(_handler));
    }

    private static GitHubApiClient CreateApiClient(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };

        return new GitHubApiClient(httpClient, NullLogger<GitHubApiClient>.Instance);
    }

    private static GitHubTetherEntry Entry(string sourcePath = "claude/rules.md", string? token = "tok_abc")
    {
        return new GitHubTetherEntry
        {
            Token = token,
            Repo = "cytoph/config",
            Path = sourcePath,
            Destination = "/tmp/rules.md",
        };
    }

    private static string Base64WithNewlines(string content)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));

        return encoded.Length > 4 ? encoded.Insert(4, "\n") : encoded;
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    #region GetLatestVersionAsync

    [Fact]
    public async Task GetLatestVersionAsync_ReturnsLatestCommitShaForPathAndSendsBearerToken()
    {
        // Arrange
        _handler.Respond("/repos/cytoph/config/commits?path=claude/rules.md&per_page=1", HttpStatusCode.OK, """[{"sha": "abc123def"}]""");

        // Act
        Result<RemoteVersion> result = await _provider.GetLatestVersionAsync(Entry());

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.VersionRef.Should().Be("abc123def");
        HttpRequestMessage request = _handler.Requests.Should().ContainSingle().Which;
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("tok_abc");
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithoutToken_SendsNoAuthHeader()
    {
        // Arrange
        _handler.Respond("/repos/cytoph/config/commits?path=claude/rules.md&per_page=1", HttpStatusCode.OK, """[{"sha": "abc123def"}]""");

        // Act
        await _provider.GetLatestVersionAsync(Entry(token: null));

        // Assert
        HttpRequestMessage request = _handler.Requests.Should().ContainSingle().Which;
        request.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_NoCommitsForPath_ReturnsFailure()
    {
        // Arrange
        _handler.Respond("/repos/cytoph/config/commits?path=claude/rules.md&per_page=1", HttpStatusCode.OK, "[]");

        // Act
        Result<RemoteVersion> result = await _provider.GetLatestVersionAsync(Entry());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("claude/rules.md");
    }

    #endregion // GetLatestVersionAsync

    #region FetchAsync

    [Fact]
    public async Task FetchAsync_PlainFile_ReturnsSingleFileWithEmptyRelativePath()
    {
        // Arrange
        string body = $$"""{"type": "file", "encoding": "base64", "content": "{{Base64WithNewlines("file content")}}"}""";
        _handler.Respond("/repos/cytoph/config/contents/claude/rules.md", HttpStatusCode.OK, body.Replace("\n", "\\n"));

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(), "abc123def");

        // Assert
        result.Success.Should().BeTrue();
        RemoteFile file = result.Value.Should().ContainSingle().Which;
        file.RelativePath.Should().BeEmpty();
        Encoding.UTF8.GetString(file.Content).Should().Be("file content");
    }

    [Fact]
    public async Task FetchAsync_LargeFile_FetchesContentFromBlobApi()
    {
        // Arrange
        _handler.Respond("/repos/cytoph/config/contents/claude/rules.md", HttpStatusCode.OK, """{"type": "file", "encoding": "none", "content": "", "sha": "blob-large"}""");
        string blobBody = $$"""{"content": "{{Base64WithNewlines("large file content")}}"}""";
        _handler.Respond("/repos/cytoph/config/git/blobs/blob-large", HttpStatusCode.OK, blobBody.Replace("\n", "\\n"));

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(), "abc123def");

        // Assert
        result.Success.Should().BeTrue();
        RemoteFile file = result.Value.Should().ContainSingle().Which;
        file.RelativePath.Should().BeEmpty();
        Encoding.UTF8.GetString(file.Content).Should().Be("large file content");
    }

    [Fact]
    public async Task FetchAsync_Folder_ReturnsFilesWithRelativePaths()
    {
        // Arrange
        _handler.Respond("/repos/cytoph/config/contents/claude/skills/my-skill", HttpStatusCode.OK, "[]");
        _handler.Respond("/repos/cytoph/config/git/trees/abc123def", HttpStatusCode.OK, $$"""
            {"tree": [
              {"type": "blob", "path": "claude/skills/my-skill/SKILL.md", "sha": "blob1"},
              {"type": "blob", "path": "claude/skills/my-skill/ref/notes.md", "sha": "blob2"},
              {"type": "blob", "path": "claude/other.md", "sha": "blob3"},
              {"type": "tree", "path": "claude/skills/my-skill/ref", "sha": "tree1"}
            ]}
            """);
        string blob1Body = $$"""{"content": "{{Base64WithNewlines("skill body")}}"}""";
        string blob2Body = $$"""{"content": "{{Base64WithNewlines("notes body")}}"}""";
        _handler.Respond("/repos/cytoph/config/git/blobs/blob1", HttpStatusCode.OK, blob1Body.Replace("\n", "\\n"));
        _handler.Respond("/repos/cytoph/config/git/blobs/blob2", HttpStatusCode.OK, blob2Body.Replace("\n", "\\n"));

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(sourcePath: "claude/skills/my-skill"), "abc123def");

        // Assert
        result.Success.Should().BeTrue();
        IReadOnlyList<RemoteFile> files = result.Value!;
        files.Should().HaveCount(2);
        files[0].RelativePath.Should().Be("SKILL.md");
        Encoding.UTF8.GetString(files[0].Content).Should().Be("skill body");
        files[1].RelativePath.Should().Be("ref/notes.md");
    }

    [Fact]
    public async Task FetchAsync_ApiError_ReturnsFailure()
    {
        // Arrange
        _handler.Respond("/repos/cytoph/config/contents/claude/rules.md", HttpStatusCode.InternalServerError, "boom");

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(), "abc123def");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("500");
    }

    [Fact]
    public async Task FetchAsync_TransportFailure_ReturnsFailure()
    {
        // Arrange
        using ThrowingHttpMessageHandler throwingHandler = new(new HttpRequestException("connection refused"));
        GitHubProvider provider = new(CreateApiClient(throwingHandler));

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await provider.FetchAsync(Entry(), "abc123def");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("connection refused");
    }

    [Fact]
    public async Task FetchAsync_CallerCancellation_PropagatesOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();
        using ThrowingHttpMessageHandler throwingHandler = new(new OperationCanceledException(cts.Token));
        GitHubProvider provider = new(CreateApiClient(throwingHandler));

        // Act
        Func<Task> act = () => provider.FetchAsync(Entry(), "abc123def", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion // FetchAsync
}
