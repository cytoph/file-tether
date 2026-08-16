using FileTether.Core.Configuration.Internal;
using FileTether.Core.Configuration.Models;

namespace FileTether.Core.Tests.Configuration;

public class ConfigValidatorTests
{
    #region Validate

    [Fact]
    public void Validate_GitHubEntryMissingRepoAndFiles_ReportsBoth()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers = [new GitHubTetherEntryConfig()],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().HaveCount(2);
        errors.Should().Contain(error => error.Contains("repo"));
        errors.Should().Contain(error => error.Contains("files"));
    }

    [Fact]
    public void Validate_GistEntryMissingIdAndDestination_ReportsBoth()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers = [new GistTetherEntryConfig()],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().HaveCount(2);
        errors.Should().Contain(error => error.Contains("id"));
        errors.Should().Contain(error => error.Contains("destination"));
    }

    [Fact]
    public void Validate_DuplicateDestinations_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GitHubTetherEntryConfig
                {
                    Repo = "cytoph/config",
                    Files = [new GitHubFileConfig { Path = "a.md", Destination = "~/same.md" }],
                },
                new GistTetherEntryConfig { Id = "abc123", Destination = "~/same.md" },
            ],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().Contain("~/same.md");
    }

    [Fact]
    public void Validate_DuplicateDestinationsDifferingOnlyByCaseOrRelativeForm_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GitHubTetherEntryConfig
                {
                    Repo = "cytoph/config",
                    Files = [new GitHubFileConfig { Path = "a.md", Destination = "~/Same.md" }],
                },
                new GistTetherEntryConfig { Id = "abc123", Destination = "~/same.md" },
            ],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            errors.Should().ContainSingle();
        }
        else
        {
            errors.Should().BeEmpty();
        }
    }

    [Fact]
    public void Validate_DuplicateNames_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GitHubTetherEntryConfig
                {
                    Repo = "cytoph/config",
                    Files = [new GitHubFileConfig { Path = "a.md", Destination = "~/a.md", Name = "shared" }],
                },
                new GistTetherEntryConfig { Id = "abc123", Destination = "~/g.md", Name = "SHARED" },
            ],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().ContainEquivalentOf("shared");
    }

    [Fact]
    public void Validate_GitHubRepoWrongFormat_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GitHubTetherEntryConfig
                {
                    Repo = "not-a-valid-repo",
                    Files = [new GitHubFileConfig { Path = "a.md", Destination = "~/a.md" }],
                },
            ],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().Contain("owner/name");
    }

    [Fact]
    public void Validate_GistIdWrongFormat_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers = [new GistTetherEntryConfig { Id = "not-hex!", Destination = "~/g.md" }],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().Contain("hexadecimal");
    }

    [Fact]
    public void Validate_GistBothDestinationAndFiles_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GistTetherEntryConfig
                {
                    Id = "abc123",
                    Destination = "~/g.md",
                    Files = [new GistFileConfig { Filename = "a.md", Destination = "~/a.md" }],
                },
            ],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().Contain("either 'destination' or 'files'");
    }

    [Fact]
    public void Validate_GistNeitherDestinationNorFiles_Reported()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers = [new GistTetherEntryConfig { Id = "abc123" }],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().Contain("'destination' or a non-empty 'files' list");
    }

    [Fact]
    public void Validate_ValidConfig_NoErrors()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GitHubTetherEntryConfig
                {
                    Token = "tok",
                    Repo = "cytoph/config",
                    Files =
                    [
                        new GitHubFileConfig { Path = "a.md", Destination = "~/a.md" },
                        new GitHubFileConfig { Path = "skills/my-skill", Destination = "~/skills/my-skill" },
                    ],
                },
                new GistTetherEntryConfig { Id = "abc123", Destination = "~/g.md" },
            ],
        };

        // Act
        List<string> errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().BeEmpty();
    }

    #endregion // Validate
}
