namespace FileTether.Core.Configuration.Models;

public sealed class GitHubTetherEntryConfig : TetherEntryConfig
{
    public string? Token { get; set; }

    public string Repo { get; set; } = "";

    public List<GitHubFileConfig> Files { get; set; } = [];
}

public sealed class GitHubFileConfig
{
    public string Path { get; set; } = "";

    public string Destination { get; set; } = "";

    public string? Name { get; set; }
}
