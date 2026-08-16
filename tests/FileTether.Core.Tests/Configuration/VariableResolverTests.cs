using FileTether.Core.Configuration.Internal;
using FileTether.Core.Configuration.Models;

namespace FileTether.Core.Tests.Configuration;

public class VariableResolverTests
{
    private static TetherConfig ConfigWithToken(string? token)
    {
        return new TetherConfig
        {
            Tethers =
            [
                new GistTetherEntryConfig { Token = token, Id = "abc123", Destination = "~/x.md" },
            ],
        };
    }

    #region ResolveAll

    [Fact]
    public void ResolveAll_ReplacesVariableFromEnvFile()
    {
        // Arrange
        VariableResolver resolver = new(new Dictionary<string, string> { ["GITHUB_TOKEN"] = "ghp_fromfile" });
        TetherConfig config = ConfigWithToken("${GITHUB_TOKEN}");

        // Act
        List<string> errors = resolver.ResolveAll(config);

        // Assert
        errors.Should().BeEmpty();
        ((GistTetherEntryConfig)config.Tethers[0]).Token.Should().Be("ghp_fromfile");
    }

    [Fact]
    public void ResolveAll_EnvFileWinsOverOsEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FILETETHER_TEST_VAR", "from_os");
        try
        {
            VariableResolver resolver = new(new Dictionary<string, string> { ["FILETETHER_TEST_VAR"] = "from_file" });
            TetherConfig config = ConfigWithToken("${FILETETHER_TEST_VAR}");

            // Act
            resolver.ResolveAll(config);

            // Assert
            ((GistTetherEntryConfig)config.Tethers[0]).Token.Should().Be("from_file");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FILETETHER_TEST_VAR", null);
        }
    }

    [Fact]
    public void ResolveAll_FallsBackToOsEnvironment()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FILETETHER_TEST_VAR", "from_os");
        try
        {
            VariableResolver resolver = new(new Dictionary<string, string>());
            TetherConfig config = ConfigWithToken("${FILETETHER_TEST_VAR}");

            // Act
            resolver.ResolveAll(config);

            // Assert
            ((GistTetherEntryConfig)config.Tethers[0]).Token.Should().Be("from_os");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FILETETHER_TEST_VAR", null);
        }
    }

    [Fact]
    public void ResolveAll_UnresolvableVariable_ReturnsErrorNamingVariableAndEntry()
    {
        // Arrange
        VariableResolver resolver = new(new Dictionary<string, string>());
        TetherConfig config = new()
        {
            Tethers =
            [
                new GistTetherEntryConfig { Token = "literal", Id = "abc123", Destination = "~/a.md" },
                new GistTetherEntryConfig { Token = "${FILETETHER_DOES_NOT_EXIST}", Id = "def456", Destination = "~/b.md" },
            ],
        };

        // Act
        List<string> errors = resolver.ResolveAll(config);

        // Assert
        string error = errors.Should().ContainSingle().Which;
        error.Should().Contain("FILETETHER_DOES_NOT_EXIST");
        error.Should().Contain("tethers[1]");
        ((GistTetherEntryConfig)config.Tethers[1]).Token.Should().Be("${FILETETHER_DOES_NOT_EXIST}");
    }

    [Fact]
    public void ResolveAll_LiteralAndNullValuesPassThrough()
    {
        // Arrange
        VariableResolver resolver = new(new Dictionary<string, string>());
        TetherConfig config = ConfigWithToken("plain_literal");

        // Act
        List<string> errors = resolver.ResolveAll(config);

        // Assert
        errors.Should().BeEmpty();
        ((GistTetherEntryConfig)config.Tethers[0]).Token.Should().Be("plain_literal");
    }

    #endregion // ResolveAll
}
