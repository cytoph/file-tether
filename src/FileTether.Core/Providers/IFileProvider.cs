using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Providers;

public interface IFileProvider
{
    string Name { get; }

    Task<Result<RemoteVersion>> GetLatestVersionAsync(TetherEntry entry, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<RemoteFile>>> FetchAsync(TetherEntry entry, string versionRef, CancellationToken cancellationToken = default);
}
