namespace FileTether.Core.Configuration.Models;

public sealed class TetherConfig
{
    public List<TetherEntryConfig> Tethers { get; set; } = [];
}

public abstract class TetherEntryConfig
{
}
