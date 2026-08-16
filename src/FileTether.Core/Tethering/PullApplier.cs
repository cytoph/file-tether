using FileTether.Core.Providers.Models;
using FileTether.Core.State.Models;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;
using Microsoft.Extensions.Logging;

namespace FileTether.Core.Tethering;

public sealed class PullApplier
{
    private readonly FileWriter _fileWriter;
    private readonly LocalFingerprintCalculator _fingerprintCalculator;
    private readonly ILogger<PullApplier> _logger;

    public PullApplier(FileWriter fileWriter, LocalFingerprintCalculator fingerprintCalculator, ILogger<PullApplier> logger)
    {
        _fileWriter = fileWriter;
        _fingerprintCalculator = fingerprintCalculator;
        _logger = logger;
    }

    public Result<StateEntry> Apply(TetherEntry entry, string versionRef, IReadOnlyList<RemoteFile> files)
    {
        try
        {
            _fileWriter.Write(entry.Destination, files);
        }
        catch (IOException exception)
        {
            return Result.Fail<StateEntry>($"Failed to write {entry.Destination}: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail<StateEntry>($"Failed to write {entry.Destination}: {exception.Message}");
        }

        Dictionary<string, string> fingerprints = ComputeRemoteFingerprints(files);

        _logger.LogInformation("Applied {Destination} at {VersionRef}", entry.Destination, versionRef);

        return new StateEntry { VersionRef = versionRef, Fingerprints = fingerprints };
    }

    public bool LocalMatches(TetherEntry entry, IReadOnlyList<RemoteFile> files)
    {
        Dictionary<string, string> local = _fingerprintCalculator.Compute(entry.Destination);
        Dictionary<string, string> remote = ComputeRemoteFingerprints(files);

        if (local.Count != remote.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> file in remote)
        {
            if (!local.TryGetValue(file.Key, out string? hash) || hash != file.Value)
            {
                return false;
            }
        }

        return true;
    }

    public StateEntry FixState(TetherEntry entry, string versionRef, IReadOnlyList<RemoteFile> files)
    {
        Dictionary<string, string> fingerprints = ComputeRemoteFingerprints(files);

        _logger.LogInformation("Fixed state for {Destination} at {VersionRef}", entry.Destination, versionRef);

        return new StateEntry { VersionRef = versionRef, Fingerprints = fingerprints };
    }

    private static Dictionary<string, string> ComputeRemoteFingerprints(IReadOnlyList<RemoteFile> files)
    {
        return files.ToDictionary(file => file.RelativePath, file => Hashing.Sha256Hex(file.Content));
    }
}
