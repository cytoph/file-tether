namespace FileTether.Core.Tethering.Models;

public sealed class EntryReport
{
    public required TetherEntry Entry { get; init; }

    public required EntryState State { get; init; }

    public string? PulledVersionRef { get; init; }

    public string? LatestVersionRef { get; init; }

    public string? Error { get; init; }

    public bool DestinationOccupied { get; init; }
}
