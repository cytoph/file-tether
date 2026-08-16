using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering.Models;

namespace FileTether.Cli.Models;

public sealed class PendingPull
{
    public required EntryReport Report { get; init; }

    public bool FixStateOnly { get; init; }

    public IReadOnlyList<RemoteFile>? PrefetchedFiles { get; init; }

    public bool IsConflict => Report.State == EntryState.Conflict || (Report.DestinationOccupied && !FixStateOnly);
}
