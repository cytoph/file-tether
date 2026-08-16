using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Providers;

public abstract class FileProvider<TEntry> : IFileProvider
    where TEntry : TetherEntry
{
    public abstract string Name { get; }

    public Task<Result<RemoteVersion>> GetLatestVersionAsync(TetherEntry entry, CancellationToken cancellationToken = default)
    {
        return GetLatestVersionAsync(AsTypedEntry(entry), cancellationToken);
    }

    public Task<Result<IReadOnlyList<RemoteFile>>> FetchAsync(TetherEntry entry, string versionRef, CancellationToken cancellationToken = default)
    {
        return FetchAsync(AsTypedEntry(entry), versionRef, cancellationToken);
    }

    protected abstract Task<Result<RemoteVersion>> GetLatestVersionAsync(TEntry entry, CancellationToken cancellationToken);

    protected abstract Task<Result<IReadOnlyList<RemoteFile>>> FetchAsync(TEntry entry, string versionRef, CancellationToken cancellationToken);

    private TEntry AsTypedEntry(TetherEntry entry)
    {
        if (entry is TEntry typedEntry)
        {
            return typedEntry;
        }

        throw new InvalidOperationException($"{GetType().Name} cannot handle tether entries of type '{entry.GetType().Name}'.");
    }
}
