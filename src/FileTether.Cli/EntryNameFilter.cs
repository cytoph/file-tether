using FileTether.Core.Tethering.Models;

namespace FileTether.Cli;

public static class EntryNameFilter
{
    public static List<TetherEntry> Filter(IReadOnlyList<TetherEntry> entries, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return [.. entries];
        }

        return [.. entries.Where(entry => string.Equals(entry.DisplayName, name, StringComparison.OrdinalIgnoreCase))];
    }

    public static string AvailableNames(IReadOnlyList<TetherEntry> entries)
    {
        return entries.Count == 0
            ? "(no entries configured)"
            : string.Join(", ", entries.Select(entry => entry.DisplayName));
    }
}
