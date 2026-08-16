using FileTether.Core;
using FileTether.Core.Configuration;
using FileTether.Core.State;
using FileTether.Core.State.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.State;

public sealed class StateStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly StateStore _store = new(NullLogger<StateStore>.Instance);

    public StateStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region Save

    [Fact]
    public void Save_ThenLoad_RoundTripsState()
    {
        // Arrange
        string path = Path.Combine(_tempDirectory, TetherPaths.StateFileName);
        TetherState state = new()
        {
            Entries =
            {
                ["abc123"] = new StateEntry
                {
                    VersionRef = "d4c3b2a1",
                    Fingerprints = new Dictionary<string, string>
                    {
                        [""] = "hash-plain-file",
                        ["sub/file.md"] = "hash-folder-file",
                    },
                },
            },
        };

        // Act
        _store.Save(path, state);
        TetherState loaded = _store.Load(path);

        // Assert
        StateEntry entry = loaded.Entries["abc123"];
        entry.VersionRef.Should().Be("d4c3b2a1");
        entry.Fingerprints[""].Should().Be("hash-plain-file");
        entry.Fingerprints["sub/file.md"].Should().Be("hash-folder-file");
    }

    #endregion // Save

    #region Load

    [Fact]
    public void Load_MissingFile_ReturnsEmptyState()
    {
        // Act
        TetherState state = _store.Load(Path.Combine(_tempDirectory, TetherPaths.StateFileName));

        // Assert
        state.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Load_CorruptJson_ThrowsInvalidOperationExceptionNamingPath()
    {
        // Arrange
        string path = Path.Combine(_tempDirectory, TetherPaths.StateFileName);
        File.WriteAllText(path, "{not valid json");

        // Act
        Action act = () => _store.Load(path);

        // Assert
        InvalidOperationException exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain(path);
    }

    [Fact]
    public void Load_EntryWithNullFingerprints_GuardsWithEmptyDictionary()
    {
        // Arrange
        string path = Path.Combine(_tempDirectory, TetherPaths.StateFileName);
        File.WriteAllText(path, """{"entries": {"abc123": {"versionRef": "v1", "fingerprints": null}}}""");

        // Act
        TetherState state = _store.Load(path);

        // Assert
        state.Entries["abc123"].Fingerprints.Should().NotBeNull();
        state.Entries["abc123"].Fingerprints.Should().BeEmpty();
    }

    #endregion // Load
}
