using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using FileTether.Cli.Models;
using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;
using Spectre.Console;

namespace FileTether.Cli;

public sealed class ConsoleRenderer
{
    private static readonly Dictionary<Type, Dictionary<Enum, string>> s_enumDisplayNames = [];

    private readonly IAnsiConsole _console;

    public ConsoleRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    #region Command rendering

    public static string DescribeChange(EntryReport report)
    {
        string target = report.Entry.DisplayName;

        return report.State switch
        {
            EntryState.New => $"{target}: new ({Shorten(report.LatestVersionRef)})",
            EntryState.Conflict => $"{target}: conflict - local changes vs {Shorten(report.LatestVersionRef)}",
            _ => $"{target}: {Shorten(report.PulledVersionRef)} -> {Shorten(report.LatestVersionRef)}",
        };
    }

    public void RenderReports(IReadOnlyList<EntryReport> reports)
    {
        if (reports.Count == 0)
        {
            _console.MarkupLine("[grey]No tethers configured.[/]");

            return;
        }

        Table table = new();

        table.AddColumn("State");
        table.AddColumn("Name");
        table.AddColumn("Destination");
        table.AddColumn("Pulled");
        table.AddColumn("Latest");

        foreach (EntryReport report in reports)
        {
            table.AddRow(
                StateMarkup(report),
                Markup.Escape(report.Entry.DisplayName),
                Markup.Escape(report.Entry.Destination),
                Markup.Escape(Shorten(report.PulledVersionRef)),
                Markup.Escape(Shorten(report.LatestVersionRef)));
        }

        _console.Write(table);

        RenderErrors(reports);
    }

    public void RenderReports(IReadOnlyList<StatusReport> reports)
    {
        if (reports.Count == 0)
        {
            _console.MarkupLine("[grey]No tethers configured.[/]");

            return;
        }

        Table table = new();

        table.AddColumn("State");
        table.AddColumn("Name");
        table.AddColumn("Destination");
        table.AddColumn("Pulled");

        foreach (StatusReport report in reports)
        {
            table.AddRow(StatusMarkup(report), Markup.Escape(report.Entry.DisplayName), Markup.Escape(report.Entry.Destination), Markup.Escape(Shorten(report.PulledVersionRef)));
        }

        _console.Write(table);
    }

    public void RenderErrors(IReadOnlyList<EntryReport> reports)
    {
        foreach (EntryReport report in reports.Where(report => report.Error is not null))
        {
            _console.MarkupLine($"[red]error[/] {Markup.Escape(report.Entry.DisplayName)}: {Markup.Escape(report.Error!)}");
        }
    }

    public void RenderEntryDiff(TetherEntry entry, IReadOnlyList<RemoteFile> files)
    {
        string destinationRoot = PathExpander.Expand(entry.Destination);

        foreach (RemoteFile file in files)
        {
            string localPath = string.IsNullOrEmpty(file.RelativePath) ? destinationRoot : Path.Combine(destinationRoot, file.RelativePath);
            string label = string.IsNullOrEmpty(file.RelativePath) ? entry.Destination : file.RelativePath;
            string remoteText = Encoding.UTF8.GetString(file.Content);
            string localText = File.Exists(localPath) ? File.ReadAllText(localPath) : string.Empty;

            if (localText == remoteText)
            {
                continue;
            }

            _console.MarkupLine($"[bold]{Markup.Escape(label)}[/]");

            if (file.Content.Contains((byte)0))
            {
                _console.MarkupLine("[grey](binary content differs)[/]");

                continue;
            }

            Grid grid = new();
            grid.AddColumn(new GridColumn { NoWrap = true, Padding = new Padding(0, 0, 1, 0) });
            grid.AddColumn(new GridColumn { Padding = new Padding(0, 0, 0, 0) });

            foreach (DiffLine line in LineDiff.Compute(localText, remoteText))
            {
                (string marker, string color) = line.Kind switch
                {
                    DiffKind.Added => ("+", "green"),
                    DiffKind.Removed => ("-", "red"),
                    _ => (" ", "grey"),
                };

                grid.AddRow($"[{color}]{marker}[/]", $"[{color}]{Markup.Escape(line.Text)}[/]");
            }

            _console.Write(grid);
        }

        HashSet<string> remotePaths = [.. files.Select(file => file.RelativePath)];

        if (Directory.Exists(destinationRoot) && remotePaths.All(path => !string.IsNullOrEmpty(path)))
        {
            foreach (string localFile in Directory.EnumerateFiles(destinationRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(destinationRoot, localFile).Replace('\\', '/');

                if (!remotePaths.Contains(relative))
                {
                    _console.MarkupLine($"[red]- {Markup.Escape(relative)} (will be removed)[/]");
                }
            }
        }
    }

    public void RenderPullSummary(IReadOnlyList<PendingPull> pendingPulls)
    {
        Table table = new();

        table.AddColumn("State");
        table.AddColumn("Name");
        table.AddColumn("Destination");
        table.AddColumn("Pulled");
        table.AddColumn("Latest");

        foreach (PendingPull pendingPull in pendingPulls)
        {
            EntryReport report = pendingPull.Report;
            table.AddRow(
                PendingStateMarkup(pendingPull),
                Markup.Escape(report.Entry.DisplayName),
                Markup.Escape(report.Entry.Destination),
                Markup.Escape(Shorten(report.PulledVersionRef)),
                Markup.Escape(Shorten(report.LatestVersionRef)));
        }

        _console.Write(table);
    }

    public bool ConfirmApply(int count)
    {
        return _console.Prompt(new ConfirmationPrompt($"Apply {count} pull(s)?") { DefaultValue = true });
    }

    public ConflictChoice PromptConflictResolution(EntryReport report)
    {
        return _console.Prompt(new SelectionPrompt<ConflictChoice>()
            .Title($"{Markup.Escape(DescribeChange(report))}\nHow do you want to resolve it?")
            .UseConverter(EnumDisplayConverter)
            .AddChoices(Enum.GetValues<ConflictChoice>()));
    }

    #endregion // Command rendering

    #region Generic rendering

    public void Success(string label, string? text = null)
    {
        RenderPrimitive("green", label, text);
    }

    public void Warning(string label, string? text = null)
    {
        RenderPrimitive("yellow", label, text);
    }

    public void Error(string label, string? text = null)
    {
        RenderPrimitive("red", label, text);
    }

    public void Line(string text)
    {
        _console.MarkupLine(Markup.Escape(text));
    }

    public Task<TResult> WithSpinnerAsync<TResult>(string message, Func<Task<TResult>> action)
    {
        return _console.Status().StartAsync(Markup.Escape(message), _ => action());
    }

    #endregion // Generic rendering

    #region Helpers

    private void RenderPrimitive(string color, string label, string? text)
    {
        string escapedLabel = Markup.Escape(label);

        if (text is null)
        {
            _console.MarkupLine($"[{color}]{escapedLabel}[/]");

            return;
        }

        _console.MarkupLine($"[{color}]{escapedLabel}[/] {Markup.Escape(text)}");
    }

    private static string PendingStateMarkup(PendingPull pendingPull)
    {
        if (pendingPull.Report.DestinationOccupied)
        {
            return pendingPull.FixStateOnly ? "[green]already present (fix state only)[/]" : "[red]existing-destination-conflict[/]";
        }

        return StateMarkup(pendingPull.Report.State);
    }

    private static string StateMarkup(EntryReport report)
    {
        if (report.State == EntryState.New && report.DestinationOccupied)
        {
            return "[yellow]new (destination occupied)[/]";
        }

        return StateMarkup(report.State);
    }

    private static string StateMarkup(EntryState state)
    {
        return state switch
        {
            EntryState.New => "[blue]new[/]",
            EntryState.UpToDate => "[green]up to date[/]",
            EntryState.PullPending => "[yellow]pull pending[/]",
            EntryState.LocallyModified => "[magenta]locally modified[/]",
            EntryState.Conflict => "[red]conflict[/]",
            EntryState.Error => "[red]error[/]",
            _ => state.ToString(),
        };
    }

    private static string StatusMarkup(StatusReport report)
    {
        if (report.PulledVersionRef is null)
        {
            return report.DestinationOccupied ? "[blue]not pulled yet (destination occupied)[/]" : "[blue]not pulled yet[/]";
        }

        return report.LocallyModified ? "[magenta]modified locally[/]" : "[green]unchanged[/]";
    }

    private static string Shorten(string? versionRef)
    {
        return versionRef is null ? "-" : versionRef.Length > 8 ? versionRef[..8] : versionRef;
    }

    private static string EnumDisplayConverter<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!s_enumDisplayNames.TryGetValue(typeof(TEnum), out Dictionary<Enum, string>? displayNames))
        {
            s_enumDisplayNames[typeof(TEnum)] = displayNames = [];

            foreach (TEnum enumValue in Enum.GetValues<TEnum>())
            {
                FieldInfo field = typeof(TEnum).GetField(enumValue.ToString())!;
                DisplayAttribute? display = field.GetCustomAttribute<DisplayAttribute>();
                displayNames[enumValue] = display?.Name ?? enumValue.ToString();
            }
        }

        return displayNames[value];
    }

    #endregion // Helpers
}
