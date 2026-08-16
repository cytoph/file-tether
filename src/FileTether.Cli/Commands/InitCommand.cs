using ConsoleAppFramework;
using FileTether.Core.Configuration;

namespace FileTether.Cli.Commands;

public sealed class InitCommand
{
    private readonly ConsoleRenderer _renderer;

    private static string ExampleConfig => $$"""
        # {{TetherPaths.AppFolderName}} configuration
        # Docs: https://github.com/cytoph/file-tether
        #
        # Example entries (remove the leading '# ' to activate):
        #
        # tethers:
        #   - type: github
        #     token: ${GITHUB_TOKEN}          # optional for public repos
        #     repo: owner/name
        #     files:
        #       - path: path/in/repo/file.md
        #         destination: ~/somewhere/file.md
        #         # name: my-file            # optional; defaults to "owner/name:path/in/repo/file.md"
        #       - path: path/in/repo/folder   # a folder - pulled as one atomic unit
        #         destination: ~/somewhere/folder

        #   - type: gist
        #     token: ${GITHUB_TOKEN}          # required for secret gists
        #     id: abc123def456
        #     filename: notes.md              # optional single-file shorthand
        #     destination: ~/somewhere/notes.md
        #     # name: my-notes               # optional; defaults to "gist:abc123def456/notes.md"
        #     # files:                       # OR: multi-file shape (mutually exclusive with filename/destination above)
        #     #   - filename: notes.md
        #     #     destination: ~/somewhere/notes.md

        tethers: []
        """;

    private static string ExampleEnv => """
        # Secrets referenced from tether.yml via ${VAR}. Keep this file out of version control.
        # GITHUB_TOKEN=ghp_...
        """;

    public InitCommand(ConsoleRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>Scaffold the config directory with a commented example tether.yml.</summary>
    /// <param name="env">Also scaffold a commented .env template (never overwrites an existing file).</param>
    /// <param name="config">-c, Path to tether.yml (default: the user config directory).</param>
    [Command("init")]
    public int Execute(bool env = false, string? config = null)
    {
        TetherPaths paths = TetherPaths.Resolve(config);

        if (File.Exists(paths.ConfigPath))
        {
            _renderer.Error("Config already exists:", paths.ConfigPath);

            return ExitCodes.ConfigError;
        }

        string configDirectory = Path.GetDirectoryName(paths.ConfigPath)!;
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(paths.ConfigPath, ExampleConfig + Environment.NewLine);

        _renderer.Success("Created", paths.ConfigPath);
        _renderer.Line($"Edit it to add tether entries, then run {TetherPaths.AppFolderName} check.");

        if (env)
        {
            if (!File.Exists(paths.EnvPath))
            {
                File.WriteAllText(paths.EnvPath, ExampleEnv + Environment.NewLine);

                _renderer.Success("Created", paths.EnvPath);
            }
        }

        return ExitCodes.Success;
    }
}
