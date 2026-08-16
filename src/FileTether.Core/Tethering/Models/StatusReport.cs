namespace FileTether.Core.Tethering.Models;

public sealed class StatusReport
{
    public required TetherEntry Entry { get; init; }

    public string? PulledVersionRef { get; init; }

    public required bool LocallyModified { get; init; }

    public bool DestinationOccupied { get; init; }
}
