namespace FileTether.Core.State.Models;

public sealed class TetherState
{
    public Dictionary<string, StateEntry> Entries { get; set; } = new();
}
