namespace FileTether.Core.Configuration;

public sealed class TetherPaths
{
    public const string AppFolderName = "file-tether";
    public const string ConfigFileName = "tether.yml";
    public const string StateFileName = ".tether-state";
    public const string EnvFileName = ".env";

    public required string ConfigPath { get; init; }

    public required string StatePath { get; init; }

    public required string EnvPath { get; init; }

    public static TetherPaths Resolve(string? configOverride)
    {
        string configPath = string.IsNullOrEmpty(configOverride)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName, ConfigFileName)
            : Path.GetFullPath(configOverride);

        string configDirectory = Path.GetDirectoryName(configPath)!;

        return new TetherPaths
        {
            ConfigPath = configPath,
            StatePath = Path.Combine(configDirectory, StateFileName),
            EnvPath = Path.Combine(configDirectory, EnvFileName),
        };
    }
}
