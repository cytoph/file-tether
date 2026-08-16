using ConsoleAppFramework;
using FileTether.Core;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Models;

namespace FileTether.Cli.Commands;

public sealed class CheckCommand
{
    private readonly TetherSession _session;
    private readonly TetherAnalyzer _analyzer;
    private readonly ConsoleRenderer _renderer;

    public CheckCommand(TetherSession session, TetherAnalyzer analyzer, ConsoleRenderer renderer)
    {
        _session = session;
        _analyzer = analyzer;
        _renderer = renderer;
    }

    /// <summary>List entries with pending pulls and conflicts; changes nothing.</summary>
    /// <param name="name">-n, Only check the entry with this display name.</param>
    /// <param name="config">-c, Path to tether.yml (default: the user config directory).</param>
    [Command("check")]
    public async Task<int> ExecuteAsync([Argument] string? name = null, string? config = null, CancellationToken cancellationToken = default)
    {
        Result loadResult = _session.Load(config);

        if (!loadResult.Success)
        {
            _renderer.Error("Config error:", loadResult.Message);

            return ExitCodes.ConfigError;
        }

        List<TetherEntry> entries = EntryNameFilter.Filter(_session.Entries, name);

        if (!string.IsNullOrEmpty(name) && entries.Count == 0)
        {
            _renderer.Error("No entry named", $"'{name}'. Available: {EntryNameFilter.AvailableNames(_session.Entries)}");

            return ExitCodes.ConfigError;
        }

        List<EntryReport> reports = await _analyzer.CheckAsync(entries, cancellationToken);

        _renderer.RenderReports(reports);

        bool pending = reports.Any(report => report.State is EntryState.New or EntryState.PullPending or EntryState.Conflict);
        bool errors = reports.Any(report => report.State == EntryState.Error);

        return pending ? ExitCodes.PendingChanges : errors ? ExitCodes.ProviderError : ExitCodes.Success;
    }
}
