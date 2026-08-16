using System.Text;
using FileTether.Core;
using FileTether.Core.Providers.Models;
using FileTether.Core.State.Models;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.Tethering;

public sealed class PullApplierTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PullApplier _applier;

    public PullApplierTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _applier = new PullApplier(
            new FileWriter(NullLogger<FileWriter>.Instance),
            new LocalFingerprintCalculator(NullLogger<LocalFingerprintCalculator>.Instance),
            NullLogger<PullApplier>.Instance);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region Apply

    [Fact]
    public void Apply_WritesContentAndReturnsStateEntryWithFingerprints()
    {
        // Arrange
        byte[] content = Encoding.UTF8.GetBytes("new content");
        GitHubTetherEntry entry = new()
        {
            Repo = "cytoph/config",
            Path = "a.md",
            Destination = Path.Combine(_tempDirectory, "a.md"),
        };
        List<RemoteFile> files = [new RemoteFile { RelativePath = "", Content = content }];

        // Act
        Result<StateEntry> result = _applier.Apply(entry, "v2", files);

        // Assert
        result.Success.Should().BeTrue();
        StateEntry stateEntry = result.Value!;
        File.ReadAllText(entry.Destination).Should().Be("new content");
        stateEntry.VersionRef.Should().Be("v2");
        stateEntry.Fingerprints[""].Should().Be(Hashing.Sha256Hex(content));
    }

    #endregion // Apply

    #region LocalMatches

    [Fact]
    public void LocalMatches_LocalContentEqualsRemote_ReturnsTrue()
    {
        // Arrange
        byte[] content = Encoding.UTF8.GetBytes("shared content");
        GitHubTetherEntry entry = new()
        {
            Repo = "cytoph/config",
            Path = "a.md",
            Destination = Path.Combine(_tempDirectory, "a.md"),
        };
        File.WriteAllBytes(entry.Destination, content);
        List<RemoteFile> files = [new RemoteFile { RelativePath = "", Content = content }];

        // Act
        bool matches = _applier.LocalMatches(entry, files);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void LocalMatches_LocalContentDiffersFromRemote_ReturnsFalse()
    {
        // Arrange
        GitHubTetherEntry entry = new()
        {
            Repo = "cytoph/config",
            Path = "a.md",
            Destination = Path.Combine(_tempDirectory, "a.md"),
        };
        File.WriteAllText(entry.Destination, "local content");
        List<RemoteFile> files = [new RemoteFile { RelativePath = "", Content = Encoding.UTF8.GetBytes("remote content") }];

        // Act
        bool matches = _applier.LocalMatches(entry, files);

        // Assert
        matches.Should().BeFalse();
    }

    #endregion // LocalMatches

    #region FixState

    [Fact]
    public void FixState_ReturnsStateEntryAndDoesNotModifyDestination()
    {
        // Arrange
        byte[] content = Encoding.UTF8.GetBytes("shared content");
        GitHubTetherEntry entry = new()
        {
            Repo = "cytoph/config",
            Path = "a.md",
            Destination = Path.Combine(_tempDirectory, "a.md"),
        };
        File.WriteAllText(entry.Destination, "marker content");
        List<RemoteFile> files = [new RemoteFile { RelativePath = "", Content = content }];

        // Act
        StateEntry stateEntry = _applier.FixState(entry, "v3", files);

        // Assert
        stateEntry.VersionRef.Should().Be("v3");
        stateEntry.Fingerprints[""].Should().Be(Hashing.Sha256Hex(content));
        File.ReadAllText(entry.Destination).Should().Be("marker content");
    }

    #endregion // FixState
}
