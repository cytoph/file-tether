namespace FileTether.Core.Tethering.Models;

public sealed class GitHubTetherEntry : TetherEntry
{
    public string? Token { get; init; }

    public required string Repo { get; init; }

    public required string Path { get; init; }

    public override string ProviderType => "github";

    public override string SourceId => Repo;

    public override string SourcePath => Path;

    public override string VersionCacheKey => $"{ProviderType}|{Repo}|{SourcePath}";

    protected override string DerivedDisplayName => $"{Repo}:{SourcePath}";
}
