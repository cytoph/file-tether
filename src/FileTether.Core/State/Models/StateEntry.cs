namespace FileTether.Core.State.Models;

public sealed class StateEntry
{
    public required string VersionRef { get; set; }

    public required Dictionary<string, string> Fingerprints { get; set; }
}
