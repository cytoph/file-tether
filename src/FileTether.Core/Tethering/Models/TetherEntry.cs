namespace FileTether.Core.Tethering.Models;

public abstract class TetherEntry
{
    public string? Name { get; init; }

    public required string Destination { get; init; }

    public abstract string ProviderType { get; }

    public abstract string SourceId { get; }

    public abstract string SourcePath { get; }

    public abstract string VersionCacheKey { get; }

    public string SourceKey => $"{SourceId}|{SourcePath}";

    public string Identity => Hashing.Sha256Hex($"{ProviderType}|{SourceKey}|{Destination}");

    public string DisplayName => Name ?? DerivedDisplayName;

    protected abstract string DerivedDisplayName { get; }
}
