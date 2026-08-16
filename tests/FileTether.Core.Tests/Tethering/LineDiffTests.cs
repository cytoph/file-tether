using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Tests.Tethering;

public class LineDiffTests
{
    #region Compute

    [Fact]
    public void Compute_MarksChangedLines()
    {
        // Act
        List<DiffLine> diff = LineDiff.Compute("a\nb\nc", "a\nx\nc");

        // Assert
        DiffKind[] expectedKinds = [DiffKind.Unchanged, DiffKind.Removed, DiffKind.Added, DiffKind.Unchanged];
        string[] expectedTexts = ["a", "b", "x", "c"];
        diff.Select(line => line.Kind).Should().Equal(expectedKinds);
        diff.Select(line => line.Text).Should().Equal(expectedTexts);
    }

    [Fact]
    public void Compute_IdenticalTexts_AllUnchanged()
    {
        // Act
        List<DiffLine> diff = LineDiff.Compute("a\r\nb", "a\nb");

        // Assert
        diff.Should().HaveCount(2);
        diff.Should().OnlyContain(line => line.Kind == DiffKind.Unchanged);
    }

    [Fact]
    public void Compute_EmptyOldText_AllAdded()
    {
        // Act
        List<DiffLine> diff = LineDiff.Compute("", "a\nb");

        // Assert
        diff.Should().HaveCount(2);
        diff.Should().OnlyContain(line => line.Kind == DiffKind.Added);
    }

    #endregion // Compute
}
