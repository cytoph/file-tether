using ConsoleAppFramework;
using FileTether.Core;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Models;

namespace FileTether.Cli.Commands;

public sealed class StatusCommand
{
    private readonly TetherSession _session;
    private readonly TetherAnalyzer _analyzer;
    private readonly ConsoleRenderer _renderer;

    public StatusCommand(TetherSession session, TetherAnalyzer analyzer, ConsoleRenderer renderer)
    {
        _session = session;
        _analyzer = analyzer;
        _renderer = renderer;
    }

    /// <summary>Show the local view: entries, pulled versions, local modifications. No network.</summary>
    /// <param name="name">-n, Only show the entry with this display name.</param>
    /// <param name="config">-c, Path to tether.yml (default: the user config directory).</param>
    [Command("status")]
    public int Execute([Argument] string? name = null, string? config = null)
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

        List<StatusReport> reports = _analyzer.Status(entries);

        _renderer.RenderReports(reports);

        return ExitCodes.Success;
    }
}
