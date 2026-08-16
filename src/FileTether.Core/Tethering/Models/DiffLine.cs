namespace FileTether.Core.Tethering.Models;

public enum DiffKind
{
    Unchanged,
    Added,
    Removed,
}

public sealed class DiffLine
{
    public required DiffKind Kind { get; init; }

    public required string Text { get; init; }
}
