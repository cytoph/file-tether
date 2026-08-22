using FileTether.Core.Providers;
using FileTether.Core.Providers.Models;
using FileTether.Core.State.Models;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;
using Microsoft.Extensions.Logging;

namespace FileTether.Core.Tethering;

public sealed class TetherAnalyzer
{
    private readonly ProviderRegistry _providerRegistry;
    private readonly LocalFingerprintCalculator _fingerprintCalculator;
    private readonly TetherSession _session;
    private readonly ILogger<TetherAnalyzer> _logger;

    public TetherAnalyzer(ProviderRegistry providerRegistry, LocalFingerprintCalculator fingerprintCalculator, TetherSession session, ILogger<TetherAnalyzer> logger)
    {
        _providerRegistry = providerRegistry;
        _fingerprintCalculator = fingerprintCalculator;
        _session = session;
        _logger = logger;
    }

    public List<StatusReport> Status(IReadOnlyList<TetherEntry> entries)
    {
        List<StatusReport> reports = [];

        foreach (TetherEntry entry in entries)
        {
            StateEntry? stateEntry = _session.State.Entries.GetValueOrDefault(entry.Identity);

            if (stateEntry is null)
            {
                reports.Add(new StatusReport { Entry = entry, LocallyModified = false, DestinationOccupied = DestinationExists(entry.Destination) });

                continue;
            }

            reports.Add(new StatusReport
            {
                Entry = entry,
                PulledVersionRef = stateEntry.VersionRef,
                LocallyModified = IsLocallyModified(entry, stateEntry),
            });
        }

        return reports;
    }

    public async Task<List<EntryReport>> CheckAsync(IReadOnlyList<TetherEntry> entries, CancellationToken cancellationToken = default)
    {
        Dictionary<string, Result<string>> latestVersions = await LookUpLatestVersionsAsync(entries, cancellationToken);
        List<EntryReport> reports = [];

        foreach (TetherEntry entry in entries)
        {
            EntryReport report = CheckEntry(entry, latestVersions[entry.VersionCacheKey]);

            reports.Add(report);
        }

        return reports;
    }

    private EntryReport CheckEntry(TetherEntry entry, Result<string> versionResult)
    {
        StateEntry? stateEntry = _session.State.Entries.GetValueOrDefault(entry.Identity);

        if (!versionResult.Success)
        {
            return new EntryReport
            {
                Entry = entry,
                State = EntryState.Error,
                PulledVersionRef = stateEntry?.VersionRef,
                Error = versionResult.Message,
            };
        }

        string latestVersion = versionResult.Value;

        if (stateEntry is null)
        {
            return new EntryReport
            {
                Entry = entry,
                State = EntryState.New,
                LatestVersionRef = latestVersion,
                DestinationOccupied = DestinationExists(entry.Destination),
            };
        }

        bool remoteChanged = latestVersion != stateEntry.VersionRef;
        bool locallyModified = IsLocallyModified(entry, stateEntry);

        EntryState entryState = (remoteChanged, locallyModified) switch
        {
            (false, false) => EntryState.UpToDate,
            (false, true) => EntryState.LocallyModified,
            (true, false) => EntryState.PullPending,
            (true, true) => EntryState.Conflict,
        };

        return new EntryReport
        {
            Entry = entry,
            State = entryState,
            PulledVersionRef = stateEntry.VersionRef,
            LatestVersionRef = latestVersion,
        };
    }

    private async Task<Dictionary<string, Result<string>>> LookUpLatestVersionsAsync(IReadOnlyList<TetherEntry> entries, CancellationToken cancellationToken)
    {
        Dictionary<string, TetherEntry> distinctSources = [];

        foreach (TetherEntry entry in entries)
        {
            distinctSources.TryAdd(entry.VersionCacheKey, entry);
        }

        List<Task<KeyValuePair<string, Result<string>>>> lookups = [.. distinctSources.Select(source => LookUpLatestVersionAsync(source.Key, source.Value, cancellationToken))];
        KeyValuePair<string, Result<string>>[] results = await Task.WhenAll(lookups);

        return new Dictionary<string, Result<string>>(results);
    }

    private async Task<KeyValuePair<string, Result<string>>> LookUpLatestVersionAsync(string cacheKey, TetherEntry entry, CancellationToken cancellationToken)
    {
        IFileProvider provider = _providerRegistry.Resolve(entry.ProviderType);
        Result<RemoteVersion> versionResult = await provider.GetLatestVersionAsync(entry, cancellationToken);

        if (!versionResult.Success)
        {
            return new KeyValuePair<string, Result<string>>(cacheKey, Result.Fail<string>(versionResult.Message));
        }

        string versionRef = versionResult.Value.VersionRef;

        _logger.LogDebug("Latest version for {CacheKey}: {VersionRef}", cacheKey, versionRef);

        return new KeyValuePair<string, Result<string>>(cacheKey, Result.Ok(versionRef));
    }

    private bool IsLocallyModified(TetherEntry entry, StateEntry stateEntry)
    {
        Dictionary<string, string> current = _fingerprintCalculator.Compute(entry.Destination);

        if (current.Count != stateEntry.Fingerprints.Count)
        {
            return true;
        }

        foreach (KeyValuePair<string, string> recorded in stateEntry.Fingerprints)
        {
            if (!current.TryGetValue(recorded.Key, out string? hash) || hash != recorded.Value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool DestinationExists(string destination)
    {
        return File.Exists(destination) || Directory.Exists(destination);
    }
}
