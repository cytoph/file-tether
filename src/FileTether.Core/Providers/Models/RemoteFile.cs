namespace FileTether.Core.Providers.Models;

public sealed class RemoteFile
{
    public required string RelativePath { get; init; }

    public required byte[] Content { get; init; }
}
