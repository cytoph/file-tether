using ConsoleAppFramework;
using FileTether.Cli.Models;
using FileTether.Core;
using FileTether.Core.Providers;
using FileTether.Core.Providers.Models;
using FileTether.Core.State.Models;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Models;

namespace FileTether.Cli.Commands;

public sealed class PullCommand
{
    private readonly TetherSession _session;
    private readonly TetherAnalyzer _analyzer;
    private readonly PullApplier _pullApplier;
    private readonly ProviderRegistry _providerRegistry;
    private readonly ConsoleRenderer _renderer;

    public PullCommand(TetherSession session, TetherAnalyzer analyzer, PullApplier pullApplier, ProviderRegistry providerRegistry, ConsoleRenderer renderer)
    {
        _session = session;
        _analyzer = analyzer;
        _pullApplier = pullApplier;
        _providerRegistry = providerRegistry;
        _renderer = renderer;
    }

    /// <summary>Apply pending pulls: one summary, one confirmation, conflicts resolved individually.</summary>
    /// <param name="name">-n, Only pull the entry with this display name.</param>
    /// <param name="config">-c, Path to tether.yml (default: the user config directory).</param>
    /// <param name="yes">-y, Apply all non-conflicted pulls without prompting; skip conflicts.</param>
    [Command("pull")]
    public async Task<int> ExecuteAsync([Argument] string? name = null, string? config = null, bool yes = false, CancellationToken cancellationToken = default)
    {
        Result loadResult = _session.Load(config);

        if (!loadResult.Success)
        {
            _renderer.Error("Config error:", loadResult.Message);

            return ExitCodes.ConfigError;
        }

        List<TetherEntry> entries = EntryNameFilter.Filter(_session.Entries, name);

        if (!string.IsNullOrEmpty(name) && entries.Count == 0)
        {
            _renderer.Error("No entry named", $"'{name}'. Available: {EntryNameFilter.AvailableNames(_session.Entries)}");

            return ExitCodes.ConfigError;
        }

        string checkingMessage = $"Checking {entries.Count} {(entries.Count == 1 ? "tether" : "tethers")}...";
        List<EntryReport> reports = await _renderer.WithSpinnerAsync(checkingMessage, () => _analyzer.CheckAsync(entries, cancellationToken));

        bool errors = reports.Any(report => report.State == EntryState.Error);

        _renderer.RenderErrors(reports);

        List<EntryReport> candidates = [.. reports.Where(report => report.State is EntryState.New or EntryState.PullPending or EntryState.Conflict)];

        List<PendingPull> failures = [];
        List<PendingPull> pendingPulls = await BuildPendingPullsAsync(candidates, failures, cancellationToken);

        if (pendingPulls.Count == 0 && failures.Count == 0)
        {
            int locallyModified = reports.Count(report => report.State == EntryState.LocallyModified);

            _renderer.Success("Everything up to date.");

            if (locallyModified > 0)
            {
                _renderer.Warning($"{locallyModified} {(locallyModified == 1 ? "entry" : "entries")} modified locally (remote unchanged).");
            }

            return errors ? ExitCodes.ProviderError : ExitCodes.Success;
        }

        _renderer.RenderPullSummary(pendingPulls);

        List<PendingPull> nonConflicted = [.. pendingPulls.Where(pendingPull => !pendingPull.IsConflict)];
        List<PendingPull> conflicts = [.. pendingPulls.Where(pendingPull => pendingPull.IsConflict)];

        List<PendingPull> skippedConflicts = [];

        if (nonConflicted.Count > 0)
        {
            bool applyNonConflicted = yes || _renderer.ConfirmApply(nonConflicted.Count);

            if (applyNonConflicted)
            {
                foreach (PendingPull pendingPull in nonConflicted)
                {
                    if (!await ApplyAsync(pendingPull, false, cancellationToken))
                    {
                        failures.Add(pendingPull);
                    }
                }
            }
        }

        foreach (PendingPull pendingPull in conflicts)
        {
            if (yes)
            {
                skippedConflicts.Add(pendingPull);

                continue;
            }

            if (!await ApplyAsync(pendingPull, true, cancellationToken))
            {
                skippedConflicts.Add(pendingPull);
            }
        }

        foreach (PendingPull skipped in skippedConflicts)
        {
            string label = skipped.Report.DestinationOccupied && !skipped.FixStateOnly ? "skipped existing destination" : "skipped conflict";

            _renderer.Warning(label, $"{skipped.Report.Entry.Destination} - resolve locally, then pull again.");
        }

        if (failures.Count > 0)
        {
            _renderer.Error($"{failures.Count} entries failed to pull.");
        }

        return errors || failures.Count > 0 ? ExitCodes.ProviderError : ExitCodes.Success;
    }

    private async Task<List<PendingPull>> BuildPendingPullsAsync(List<EntryReport> candidates, List<PendingPull> failures, CancellationToken cancellationToken)
    {
        List<PendingPull> pendingPulls = [];

        foreach (EntryReport report in candidates)
        {
            if (!report.DestinationOccupied)
            {
                pendingPulls.Add(new PendingPull { Report = report });

                continue;
            }

            IFileProvider provider = _providerRegistry.Resolve(report.Entry.ProviderType);
            Result<IReadOnlyList<RemoteFile>> fetchResult = await provider.FetchAsync(report.Entry, report.LatestVersionRef!, cancellationToken);

            if (!fetchResult.Success)
            {
                _renderer.Error("error", $"{report.Entry.Destination}: {fetchResult.Message}");
                failures.Add(new PendingPull { Report = report });

                continue;
            }

            IReadOnlyList<RemoteFile> files = fetchResult.Value;
            bool fixStateOnly = _pullApplier.LocalMatches(report.Entry, files);

            pendingPulls.Add(new PendingPull { Report = report, FixStateOnly = fixStateOnly, PrefetchedFiles = files });
        }

        return pendingPulls;
    }

    private async Task<bool> ApplyAsync(PendingPull pendingPull, bool resolveConflict, CancellationToken cancellationToken)
    {
        EntryReport report = pendingPull.Report;
        IReadOnlyList<RemoteFile> files;

        if (pendingPull.PrefetchedFiles is { } prefetchedFiles)
        {
            files = prefetchedFiles;
        }
        else
        {
            IFileProvider provider = _providerRegistry.Resolve(report.Entry.ProviderType);
            Result<IReadOnlyList<RemoteFile>> fetchResult = await provider.FetchAsync(report.Entry, report.LatestVersionRef!, cancellationToken);

            if (!fetchResult.Success)
            {
                _renderer.Error("error", $"{report.Entry.Destination}: {fetchResult.Message}");

                return false;
            }

            files = fetchResult.Value;
        }

        while (resolveConflict)
        {
            ConflictChoice choice = _renderer.PromptConflictResolution(report);

            if (choice == ConflictChoice.ShowDiff)
            {
                _renderer.RenderEntryDiff(report.Entry, files);

                continue;
            }

            if (choice == ConflictChoice.Skip)
            {
                return false;
            }

            break;
        }

        return ApplyPull(pendingPull, files);
    }

    private bool ApplyPull(PendingPull pendingPull, IReadOnlyList<RemoteFile> files)
    {
        EntryReport report = pendingPull.Report;
        Result<StateEntry> applyResult = pendingPull.FixStateOnly
            ? Result.Ok(_pullApplier.FixState(report.Entry, report.LatestVersionRef!, files))
            : _pullApplier.Apply(report.Entry, report.LatestVersionRef!, files);

        if (!applyResult.Success)
        {
            _renderer.Error("error", $"{report.Entry.Destination}: {applyResult.Message}");

            return false;
        }

        _session.State.Entries[report.Entry.Identity] = applyResult.Value;
        _session.SaveState();

        _renderer.Success(pendingPull.FixStateOnly ? "state fixed" : "applied", report.Entry.DisplayName);

        return true;
    }
}
