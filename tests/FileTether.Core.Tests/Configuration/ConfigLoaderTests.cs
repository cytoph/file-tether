using FileTether.Core;
using FileTether.Core.Configuration;
using FileTether.Core.Configuration.Internal;
using FileTether.Core.Configuration.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.Configuration;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ConfigLoader _loader = new(new EnvFileLoader(NullLogger<EnvFileLoader>.Instance), NullLogger<ConfigLoader>.Instance);

    public ConfigLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    private (string ConfigPath, string EnvPath) WriteTempConfig(string yaml, string? envContent = null)
    {
        string configPath = Path.Combine(_tempDirectory, TetherPaths.ConfigFileName);
        string envPath = Path.Combine(_tempDirectory, TetherPaths.EnvFileName);
        File.WriteAllText(configPath, yaml);

        if (envContent is not null)
        {
            File.WriteAllText(envPath, envContent);
        }

        return (configPath, envPath);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region Load

    [Fact]
    public void Load_ResolvesVariablesFromSiblingEnvFile()
    {
        // Arrange
        (string configPath, string envPath) = WriteTempConfig("""
            tethers:
              - type: gist
                token: ${GITHUB_TOKEN}
                id: abc123
                destination: ~/g.md
            """,
            "GITHUB_TOKEN=ghp_resolved");

        // Act
        Result<TetherConfig> result = _loader.Load(configPath, envPath);

        // Assert
        result.Success.Should().BeTrue();
        ((GistTetherEntryConfig)result.Value!.Tethers[0]).Token.Should().Be("ghp_resolved");
    }

    [Fact]
    public void Load_CollectsResolutionAndValidationErrorsIntoOneException()
    {
        // Arrange
        (string configPath, string envPath) = WriteTempConfig("""
            tethers:
              - type: gist
                token: ${FILETETHER_DOES_NOT_EXIST}
                destination: ~/g.md
            """);

        // Act
        Result<TetherConfig> result = _loader.Load(configPath, envPath);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("FILETETHER_DOES_NOT_EXIST");
        result.Message.Should().Contain("id");
    }

    [Fact]
    public void Load_MissingConfigFile_ReturnsFailure()
    {
        // Arrange
        string missingPath = Path.Combine(_tempDirectory, TetherPaths.ConfigFileName);
        string envPath = Path.Combine(_tempDirectory, TetherPaths.EnvFileName);

        // Act
        Result<TetherConfig> result = _loader.Load(missingPath, envPath);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    #endregion // Load
}
