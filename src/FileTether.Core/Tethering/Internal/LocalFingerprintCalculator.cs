using Microsoft.Extensions.Logging;

namespace FileTether.Core.Tethering.Internal;

public sealed class LocalFingerprintCalculator
{
    private readonly ILogger<LocalFingerprintCalculator> _logger;

    public LocalFingerprintCalculator(ILogger<LocalFingerprintCalculator> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, string> Compute(string destination)
    {
        Dictionary<string, string> fingerprints = [];

        if (File.Exists(destination))
        {
            fingerprints[""] = Sha256HexOfFile(destination);
        }
        else if (Directory.Exists(destination))
        {
            foreach (string filePath in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(destination, filePath).Replace('\\', '/');
                fingerprints[relativePath] = Sha256HexOfFile(filePath);
            }
        }

        _logger.LogDebug("Fingerprinted {Count} file(s) at {Destination}", fingerprints.Count, destination);

        return fingerprints;
    }

    private static string Sha256HexOfFile(string path)
    {
        return Hashing.Sha256Hex(File.ReadAllBytes(path));
    }
}
