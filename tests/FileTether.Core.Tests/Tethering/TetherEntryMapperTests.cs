using FileTether.Core.Configuration.Models;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Tests.Tethering;

public class TetherEntryMapperTests
{
    #region Map

    [Fact]
    public void Map_ProducesOneEntryPerFileMappingAndPerPlainEntry()
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
        List<TetherEntry> entries = TetherEntryMapper.Map(config);

        // Assert
        entries.Should().HaveCount(3);
        GitHubTetherEntry first = entries[0].Should().BeOfType<GitHubTetherEntry>().Which;
        first.ProviderType.Should().Be("github");
        first.Token.Should().Be("tok");
        first.Repo.Should().Be("cytoph/config");
        first.SourcePath.Should().Be("a.md");
        first.Destination.Should().Be(PathExpander.Expand("~/a.md"));

        GitHubTetherEntry second = entries[1].Should().BeOfType<GitHubTetherEntry>().Which;
        second.SourcePath.Should().Be("skills/my-skill");

        GistTetherEntry third = entries[2].Should().BeOfType<GistTetherEntry>().Which;
        third.ProviderType.Should().Be("gist");
        third.GistId.Should().Be("abc123");
        third.SourcePath.Should().BeEmpty();
        third.Destination.Should().Be(PathExpander.Expand("~/g.md"));
    }

    [Fact]
    public void Map_GistWithFilesList_ProducesOneEntryPerFile()
    {
        // Arrange
        TetherConfig config = new()
        {
            Tethers =
            [
                new GistTetherEntryConfig
                {
                    Token = "tok",
                    Id = "abc123",
                    Files =
                    [
                        new GistFileConfig { Filename = "a.md", Destination = "~/a.md" },
                        new GistFileConfig { Filename = "b.md", Destination = "~/b.md", Name = "my-b" },
                    ],
                },
            ],
        };

        // Act
        List<TetherEntry> entries = TetherEntryMapper.Map(config);

        // Assert
        entries.Should().HaveCount(2);
        GistTetherEntry first = entries[0].Should().BeOfType<GistTetherEntry>().Which;
        first.Token.Should().Be("tok");
        first.GistId.Should().Be("abc123");
        first.Filename.Should().Be("a.md");
        first.Destination.Should().Be(PathExpander.Expand("~/a.md"));

        GistTetherEntry second = entries[1].Should().BeOfType<GistTetherEntry>().Which;
        second.Filename.Should().Be("b.md");
        second.Name.Should().Be("my-b");
    }

    #endregion // Map

    #region SourceKey and VersionCacheKey

    [Fact]
    public void SourceKeyAndVersionCacheKey_ComposeFromSourceFields()
    {
        // Arrange
        GitHubTetherEntry repoEntry = new()
        {
            Repo = "cytoph/config",
            Path = "a.md",
            Destination = "/tmp/a.md",
        };
        GistTetherEntry gistEntry = new()
        {
            GistId = "abc123",
            Destination = "/tmp/g.md",
        };

        // Assert
        repoEntry.SourceKey.Should().Be("cytoph/config|a.md");
        repoEntry.VersionCacheKey.Should().Be("github|cytoph/config|a.md");
        gistEntry.SourceKey.Should().Be("abc123|");
        gistEntry.VersionCacheKey.Should().Be("gist|abc123");
    }

    #endregion // SourceKey and VersionCacheKey

    #region DisplayName

    [Fact]
    public void DisplayName_DerivesFromSourceWhenNameNotSet()
    {
        // Arrange
        GitHubTetherEntry githubEntry = new()
        {
            Repo = "cytoph/config",
            Path = "a.md",
            Destination = "/tmp/a.md",
        };
        GistTetherEntry gistEntry = new()
        {
            GistId = "abc123",
            Filename = "notes.md",
            Destination = "/tmp/g.md",
        };
        GistTetherEntry namedGistEntry = new()
        {
            Name = "my notes",
            GistId = "abc123",
            Destination = "/tmp/g.md",
        };

        // Assert
        githubEntry.DisplayName.Should().Be("cytoph/config:a.md");
        gistEntry.DisplayName.Should().Be("gist:abc123/notes.md");
        namedGistEntry.DisplayName.Should().Be("my notes");
    }

    #endregion // DisplayName
}
