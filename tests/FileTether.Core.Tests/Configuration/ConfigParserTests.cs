using FileTether.Core;
using FileTether.Core.Configuration.Internal;
using FileTether.Core.Configuration.Models;

namespace FileTether.Core.Tests.Configuration;

public class ConfigParserTests
{
    #region Parse

    [Fact]
    public void Parse_FullConfig_MapsAllFields()
    {
        // Arrange
        string yaml = """
            tethers:
              - type: github
                token: ${GITHUB_TOKEN}
                repo: cytoph/config
                files:
                  - path: claude/shared-rules.md
                    destination: ~/.claude/shared-rules.md
                  - path: claude/skills/my-skill
                    destination: ~/.claude/skills/my-skill
                    name: my-skill
              - type: gist
                token: ${GITHUB_TOKEN}
                id: abc123def456
                destination: ~/.claude/gist-file.md
                name: my-gist
            """;

        // Act
        Result<TetherConfig> result = ConfigParser.Parse(yaml);

        // Assert
        result.Success.Should().BeTrue();
        TetherConfig config = result.Value!;
        config.Tethers.Should().HaveCount(2);

        GitHubTetherEntryConfig githubTether = config.Tethers[0].Should().BeOfType<GitHubTetherEntryConfig>().Which;
        githubTether.Token.Should().Be("${GITHUB_TOKEN}");
        githubTether.Repo.Should().Be("cytoph/config");
        githubTether.Files.Should().NotBeNull();
        githubTether.Files!.Should().HaveCount(2);
        githubTether.Files![0].Path.Should().Be("claude/shared-rules.md");
        githubTether.Files![0].Destination.Should().Be("~/.claude/shared-rules.md");
        githubTether.Files![0].Name.Should().BeNull();
        githubTether.Files![1].Name.Should().Be("my-skill");

        GistTetherEntryConfig gistTether = config.Tethers[1].Should().BeOfType<GistTetherEntryConfig>().Which;
        gistTether.Id.Should().Be("abc123def456");
        gistTether.Destination.Should().Be("~/.claude/gist-file.md");
        gistTether.Name.Should().Be("my-gist");
    }

    [Fact]
    public void Parse_InvalidYaml_ReturnsFailure()
    {
        // Act
        Result<TetherConfig> result = ConfigParser.Parse("tethers: [");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid YAML");
    }

    [Fact]
    public void Parse_EmptyDocument_ReturnsEmptyConfig()
    {
        // Act
        Result<TetherConfig> result = ConfigParser.Parse("");

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.Tethers.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnknownTetherType_ReturnsFailure()
    {
        // Arrange
        string yaml = """
            tethers:
              - type: svn
                destination: ~/x.md
            """;

        // Act
        Result<TetherConfig> result = ConfigParser.Parse(yaml);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("type");
        result.Message.Should().Contain("github");
        result.Message.Should().Contain("gist");
    }

    #endregion // Parse
}
