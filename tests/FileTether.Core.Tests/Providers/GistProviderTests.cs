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

public sealed class GistProviderTests : IDisposable
{
    private readonly StubHttpMessageHandler _handler = new();
    private readonly GistProvider _provider;

    public GistProviderTests()
    {
        HttpClient httpClient = new(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };

        _provider = new GistProvider(new GitHubApiClient(httpClient, NullLogger<GitHubApiClient>.Instance));
    }

    private static GistTetherEntry Entry(string? filename = null)
    {
        return new GistTetherEntry
        {
            Token = "tok_abc",
            GistId = "abc123",
            Filename = filename,
            Destination = "/tmp/gist.md",
        };
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    #region GetLatestVersionAsync

    [Fact]
    public async Task GetLatestVersionAsync_ReturnsLatestHistoryVersion()
    {
        // Arrange
        _handler.Respond("/gists/abc123", HttpStatusCode.OK, """
            {"history": [{"version": "rev-new"}, {"version": "rev-old"}]}
            """);

        // Act
        Result<RemoteVersion> result = await _provider.GetLatestVersionAsync(Entry());

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.VersionRef.Should().Be("rev-new");
    }

    #endregion // GetLatestVersionAsync

    #region FetchAsync

    [Fact]
    public async Task FetchAsync_SingleFileGist_ReturnsContentWithEmptyRelativePath()
    {
        // Arrange
        _handler.Respond("/gists/abc123/rev-new", HttpStatusCode.OK, """
            {"files": {"notes.md": {"truncated": false, "content": "gist content"}}}
            """);

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(), "rev-new");

        // Assert
        result.Success.Should().BeTrue();
        RemoteFile file = result.Value.Should().ContainSingle().Which;
        file.RelativePath.Should().BeEmpty();
        Encoding.UTF8.GetString(file.Content).Should().Be("gist content");
    }

    [Fact]
    public async Task FetchAsync_NamedFile_SelectsIt()
    {
        // Arrange
        _handler.Respond("/gists/abc123/rev-new", HttpStatusCode.OK, """
            {"files": {
              "a.md": {"truncated": false, "content": "content a"},
              "b.md": {"truncated": false, "content": "content b"}
            }}
            """);

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(filename: "b.md"), "rev-new");

        // Assert
        result.Success.Should().BeTrue();
        Encoding.UTF8.GetString(result.Value![0].Content).Should().Be("content b");
    }

    [Fact]
    public async Task FetchAsync_MultiFileGistWithoutFilename_ReturnsFailureListingFiles()
    {
        // Arrange
        _handler.Respond("/gists/abc123/rev-new", HttpStatusCode.OK, """
            {"files": {
              "a.md": {"truncated": false, "content": "content a"},
              "b.md": {"truncated": false, "content": "content b"}
            }}
            """);

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(), "rev-new");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("a.md");
        result.Message.Should().Contain("b.md");
    }

    [Fact]
    public async Task FetchAsync_TruncatedFile_FetchesRawUrl()
    {
        // Arrange
        _handler.Respond("/gists/abc123/rev-new", HttpStatusCode.OK, """
            {"files": {"big.md": {"truncated": true, "content": "partial", "raw_url": "https://gist.githubusercontent.com/raw/big.md"}}}
            """);
        _handler.Respond("/raw/big.md", HttpStatusCode.OK, "full content");

        // Act
        Result<IReadOnlyList<RemoteFile>> result = await _provider.FetchAsync(Entry(), "rev-new");

        // Assert
        result.Success.Should().BeTrue();
        Encoding.UTF8.GetString(result.Value![0].Content).Should().Be("full content");
    }

    #endregion // FetchAsync
}
