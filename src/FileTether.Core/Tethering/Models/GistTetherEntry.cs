namespace FileTether.Core.Tethering.Models;

public sealed class GistTetherEntry : TetherEntry
{
    public string? Token { get; init; }

    public required string GistId { get; init; }

    public string? Filename { get; init; }

    public override string ProviderType => "gist";

    public override string SourceId => GistId;

    public override string SourcePath => Filename ?? "";

    public override string VersionCacheKey => $"{ProviderType}|{GistId}";

    protected override string DerivedDisplayName => $"gist:{GistId}/{Filename ?? SourcePath}";
}
