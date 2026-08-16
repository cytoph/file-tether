using FileTether.Core.Providers.Models;
using Microsoft.Extensions.Logging;

namespace FileTether.Core.Tethering.Internal;

public sealed class FileWriter
{
    private readonly ILogger<FileWriter> _logger;

    public FileWriter(ILogger<FileWriter> logger)
    {
        _logger = logger;
    }

    public void Write(string destination, IReadOnlyList<RemoteFile> files)
    {
        foreach (RemoteFile file in files)
        {
            ValidateRelativePath(file.RelativePath);
        }

        if (files.Count == 1 && string.IsNullOrEmpty(files[0].RelativePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            File.WriteAllBytes(destination, files[0].Content);
        }
        else
        {
            WriteFolder(destination, files);
        }

        _logger.LogDebug("Wrote {Count} file(s) to {Destination}", files.Count, destination);
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return;
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Refusing to write file with rooted relative path '{relativePath}'.");
        }

        string[] segments = relativePath.Split('/', '\\');

        if (segments.Contains(".."))
        {
            throw new InvalidOperationException($"Refusing to write file with path traversal segment in '{relativePath}'.");
        }
    }

    // Move-aside/move-in/delete-old order: the destination is restorable until the new copy is fully in
    // place, so a crash or lock failure mid-write never leaves the destination missing or half-written.
    private static void WriteFolder(string destination, IReadOnlyList<RemoteFile> files)
    {
        string temporaryDirectory = destination + ".tether-tmp";

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        foreach (RemoteFile file in files)
        {
            string filePath = Path.Combine(temporaryDirectory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllBytes(filePath, file.Content);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);

        string oldDirectory = destination + ".tether-old";

        if (Directory.Exists(oldDirectory))
        {
            Directory.Delete(oldDirectory, recursive: true);
        }

        bool hadExistingDestination = Directory.Exists(destination);

        if (hadExistingDestination)
        {
            Directory.Move(destination, oldDirectory);
        }

        try
        {
            Directory.Move(temporaryDirectory, destination);
        }
        catch
        {
            if (hadExistingDestination)
            {
                Directory.Move(oldDirectory, destination);
            }

            throw;
        }

        if (hadExistingDestination)
        {
            Directory.Delete(oldDirectory, recursive: true);
        }
    }
}
