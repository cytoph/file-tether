using FileTether.Core.Configuration;
using FileTether.Core.Configuration.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.Configuration;

public sealed class EnvFileLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly EnvFileLoader _loader = new(NullLogger<EnvFileLoader>.Instance);

    public EnvFileLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region Load

    [Fact]
    public void Load_ParsesEntriesSkippingCommentsAndBlankLines()
    {
        // Arrange
        string path = Path.Combine(_tempDirectory, TetherPaths.EnvFileName);
        File.WriteAllText(path, """
            # a comment
            GITHUB_TOKEN=ghp_abc123

            QUOTED="with spaces"
            malformed line without equals
            """);

        // Act
        Dictionary<string, string> variables = _loader.Load(path);

        // Assert
        variables.Should().HaveCount(2);
        variables["GITHUB_TOKEN"].Should().Be("ghp_abc123");
        variables["QUOTED"].Should().Be("with spaces");
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        // Act
        Dictionary<string, string> variables = _loader.Load(Path.Combine(_tempDirectory, TetherPaths.EnvFileName));

        // Assert
        variables.Should().BeEmpty();
    }

    #endregion // Load
}
