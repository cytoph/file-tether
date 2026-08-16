using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Tethering.Internal;

public static class LineDiff
{
    public static List<DiffLine> Compute(string oldText, string newText)
    {
        string[] oldLines = SplitLines(oldText);
        string[] newLines = SplitLines(newText);

        // Longest common subsequence table, built bottom-up from the end of both sequences.
        int[,] lcs = new int[oldLines.Length + 1, newLines.Length + 1];

        for (int i = oldLines.Length - 1; i >= 0; i--)
        {
            for (int j = newLines.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = oldLines[i] == newLines[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        // Emit the minimal diff by walking the LCS table from the start, following whichever
        // neighbor kept the longer common subsequence.
        List<DiffLine> diff = [];

        int oldIndex = 0;
        int newIndex = 0;

        while (oldIndex < oldLines.Length && newIndex < newLines.Length)
        {
            if (oldLines[oldIndex] == newLines[newIndex])
            {
                diff.Add(new DiffLine { Kind = DiffKind.Unchanged, Text = oldLines[oldIndex] });
                oldIndex++;
                newIndex++;
            }
            else if (lcs[oldIndex + 1, newIndex] >= lcs[oldIndex, newIndex + 1])
            {
                diff.Add(new DiffLine { Kind = DiffKind.Removed, Text = oldLines[oldIndex] });
                oldIndex++;
            }
            else
            {
                diff.Add(new DiffLine { Kind = DiffKind.Added, Text = newLines[newIndex] });
                newIndex++;
            }
        }

        while (oldIndex < oldLines.Length)
        {
            diff.Add(new DiffLine { Kind = DiffKind.Removed, Text = oldLines[oldIndex] });
            oldIndex++;
        }

        while (newIndex < newLines.Length)
        {
            diff.Add(new DiffLine { Kind = DiffKind.Added, Text = newLines[newIndex] });
            newIndex++;
        }

        return diff;
    }

    private static string[] SplitLines(string text)
    {
        return string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n").Split('\n');
    }
}
