using FileTether.Core;
using FileTether.Core.Configuration;
using FileTether.Core.Configuration.Internal;
using FileTether.Core.Providers;
using FileTether.Core.Providers.Models;
using FileTether.Core.State;
using FileTether.Core.State.Models;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FileTether.Core.Tests.Tethering;

public sealed class TetherAnalyzerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _configPath;
    private readonly StateStore _stateStore = new(NullLogger<StateStore>.Instance);
    private readonly TetherSession _session;
    private readonly IFileProvider _provider = Substitute.For<IFileProvider>();
    private readonly TetherAnalyzer _engine;

    public TetherAnalyzerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
        _configPath = Path.Combine(_tempDirectory, TetherPaths.ConfigFileName);
        File.WriteAllText(_configPath, "tethers: []");
        _provider.Name.Returns(GitHubProvider.ProviderName);
        _session = new TetherSession(new ConfigLoader(new EnvFileLoader(NullLogger<EnvFileLoader>.Instance), NullLogger<ConfigLoader>.Instance), _stateStore);
        _engine = new TetherAnalyzer(
            new ProviderRegistry([_provider]),
            new LocalFingerprintCalculator(NullLogger<LocalFingerprintCalculator>.Instance),
            _session,
            NullLogger<TetherAnalyzer>.Instance);
    }

    private GitHubTetherEntry Entry(string fileName = "a.md", string repo = "cytoph/config")
    {
        return new GitHubTetherEntry
        {
            Repo = repo,
            Path = fileName,
            Destination = Path.Combine(_tempDirectory, fileName),
        };
    }

    private void LoadEmptyState()
    {
        LoadState(new TetherState());
    }

    private void LoadState(TetherEntry entry, string versionRef, string recordedContent)
    {
        TetherState state = new()
        {
            Entries =
            {
                [entry.Identity] = new StateEntry
                {
                    VersionRef = versionRef,
                    Fingerprints = new Dictionary<string, string> { [""] = Hashing.Sha256Hex(recordedContent) },
                },
            },
        };

        LoadState(state);
    }

    private void LoadState(TetherState state)
    {
        TetherPaths paths = TetherPaths.Resolve(_configPath);

        _stateStore.Save(paths.StatePath, state);

        Result loadResult = _session.Load(_configPath);

        loadResult.Success.Should().BeTrue();
    }

    private void SetRemoteVersion(string versionRef)
    {
        _provider.GetLatestVersionAsync(Arg.Any<TetherEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new RemoteVersion { VersionRef = versionRef }));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region CheckAsync

    [Fact]
    public async Task CheckAsync_NeverPulledEntry_ReportsNew()
    {
        // Arrange
        SetRemoteVersion("v1");
        LoadEmptyState();

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([Entry()]);

        // Assert
        reports[0].State.Should().Be(EntryState.New);
        reports[0].LatestVersionRef.Should().Be("v1");
    }

    [Fact]
    public async Task CheckAsync_SameVersionUnmodified_ReportsUpToDate()
    {
        // Arrange
        TetherEntry entry = Entry();
        File.WriteAllText(entry.Destination, "content");
        SetRemoteVersion("v1");
        LoadState(entry, "v1", "content");

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([entry]);

        // Assert
        reports[0].State.Should().Be(EntryState.UpToDate);
    }

    [Fact]
    public async Task CheckAsync_NewRemoteVersionUnmodified_ReportsPullPending()
    {
        // Arrange
        TetherEntry entry = Entry();
        File.WriteAllText(entry.Destination, "content");
        SetRemoteVersion("v2");
        LoadState(entry, "v1", "content");

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([entry]);

        // Assert
        reports[0].State.Should().Be(EntryState.PullPending);
        reports[0].PulledVersionRef.Should().Be("v1");
        reports[0].LatestVersionRef.Should().Be("v2");
    }

    [Fact]
    public async Task CheckAsync_SameVersionLocalEdit_ReportsLocallyModified()
    {
        // Arrange
        TetherEntry entry = Entry();
        File.WriteAllText(entry.Destination, "edited locally");
        SetRemoteVersion("v1");
        LoadState(entry, "v1", "content");

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([entry]);

        // Assert
        reports[0].State.Should().Be(EntryState.LocallyModified);
    }

    [Fact]
    public async Task CheckAsync_NewRemoteVersionAndLocalEdit_ReportsConflict()
    {
        // Arrange
        TetherEntry entry = Entry();
        File.WriteAllText(entry.Destination, "edited locally");
        SetRemoteVersion("v2");
        LoadState(entry, "v1", "content");

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([entry]);

        // Assert
        reports[0].State.Should().Be(EntryState.Conflict);
    }

    [Fact]
    public async Task CheckAsync_NeverPulledEntryWithExistingDestination_ReportsDestinationOccupied()
    {
        // Arrange
        TetherEntry entry = Entry();
        File.WriteAllText(entry.Destination, "content");
        SetRemoteVersion("v1");
        LoadEmptyState();

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([entry]);

        // Assert
        reports[0].State.Should().Be(EntryState.New);
        reports[0].DestinationOccupied.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_NeverPulledEntryWithNoDestination_ReportsDestinationNotOccupied()
    {
        // Arrange
        SetRemoteVersion("v1");
        LoadEmptyState();

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([Entry()]);

        // Assert
        reports[0].State.Should().Be(EntryState.New);
        reports[0].DestinationOccupied.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ProviderFailure_ReportsErrorAndContinues()
    {
        // Arrange
        _provider.GetLatestVersionAsync(Arg.Any<TetherEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<RemoteVersion>("boom"));
        LoadEmptyState();

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([Entry("a.md"), Entry("b.md", repo: "cytoph/other")]);

        // Assert
        reports.Should().HaveCount(2);
        reports.Should().OnlyContain(report => report.State == EntryState.Error);
        reports[0].Error.Should().Be("boom");
    }

    [Fact]
    public async Task CheckAsync_TwoEntriesSameRepoAndPath_LooksUpVersionOnce()
    {
        // Arrange
        SetRemoteVersion("v1");
        LoadEmptyState();
        GitHubTetherEntry first = Entry("a.md");
        GitHubTetherEntry second = new()
        {
            Repo = first.Repo,
            Path = first.SourcePath,
            Destination = Path.Combine(_tempDirectory, "other-destination", "a.md"),
        };

        // Act
        await _engine.CheckAsync([first, second]);

        // Assert
        await _provider.Received(1).GetLatestVersionAsync(Arg.Any<TetherEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_TwoEntriesDistinctSources_LooksUpVersionsInParallel()
    {
        // Arrange
        LoadEmptyState();
        int startedLookups = 0;
        TaskCompletionSource bothStarted = new();
        _provider.GetLatestVersionAsync(Arg.Any<TetherEntry>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                if (Interlocked.Increment(ref startedLookups) == 2)
                {
                    bothStarted.SetResult();
                }

                // sequential lookups would let the first call time out here instead of ever starting the second
                await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                return Result.Ok(new RemoteVersion { VersionRef = "v1" });
            });

        // Act
        List<EntryReport> reports = await _engine.CheckAsync([Entry("a.md"), Entry("b.md")]);

        // Assert
        reports.Should().OnlyContain(report => report.State == EntryState.New);
    }

    #endregion // CheckAsync

    #region Status

    [Fact]
    public async Task Status_UsesOnlyLocalData()
    {
        // Arrange
        TetherEntry pulled = Entry("a.md");
        File.WriteAllText(pulled.Destination, "content");
        TetherEntry neverPulled = Entry("b.md");
        LoadState(pulled, "v1", "content");

        // Act
        List<StatusReport> reports = _engine.Status([pulled, neverPulled]);

        // Assert
        reports[0].PulledVersionRef.Should().Be("v1");
        reports[0].LocallyModified.Should().BeFalse();
        reports[1].PulledVersionRef.Should().BeNull();
        await _provider.DidNotReceive().GetLatestVersionAsync(Arg.Any<TetherEntry>(), Arg.Any<CancellationToken>());
    }

    #endregion // Status
}
