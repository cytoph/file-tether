namespace FileTether.Core.Configuration.Models;

public sealed class GistTetherEntryConfig : TetherEntryConfig
{
    public string? Token { get; set; }

    public string Id { get; set; } = "";

    public string? Filename { get; set; }

    public string Destination { get; set; } = "";

    public string? Name { get; set; }

    public List<GistFileConfig>? Files { get; set; }
}

public sealed class GistFileConfig
{
    public string Filename { get; set; } = "";

    public string Destination { get; set; } = "";

    public string? Name { get; set; }
}
