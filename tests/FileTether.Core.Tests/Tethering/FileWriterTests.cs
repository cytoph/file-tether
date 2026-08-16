using System.Text;
using FileTether.Core.Providers.Models;
using FileTether.Core.Tethering.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.Tethering;

public sealed class FileWriterTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FileWriter _writer = new(NullLogger<FileWriter>.Instance);

    public FileWriterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region Write

    [Fact]
    public void Write_PlainFile_CreatesParentDirectories()
    {
        // Arrange
        string destination = Path.Combine(_tempDirectory, "nested", "deep", "a.md");

        // Act
        _writer.Write(destination, [new RemoteFile { RelativePath = "", Content = Encoding.UTF8.GetBytes("content") }]);

        // Assert
        File.ReadAllText(destination).Should().Be("content");
    }

    [Fact]
    public void Write_Folder_ReplacesWholesaleAndLeavesNoTempDirectory()
    {
        // Arrange
        string destination = Path.Combine(_tempDirectory, "skill");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "stale.md"), "stale local file");

        // Act
        _writer.Write(destination,
        [
            new RemoteFile { RelativePath = "SKILL.md", Content = Encoding.UTF8.GetBytes("skill") },
            new RemoteFile { RelativePath = "ref/notes.md", Content = Encoding.UTF8.GetBytes("notes") },
        ]);

        // Assert
        File.ReadAllText(Path.Combine(destination, "SKILL.md")).Should().Be("skill");
        File.ReadAllText(Path.Combine(destination, "ref", "notes.md")).Should().Be("notes");
        File.Exists(Path.Combine(destination, "stale.md")).Should().BeFalse();
        Directory.Exists(destination + ".tether-tmp").Should().BeFalse();
        Directory.Exists(destination + ".tether-old").Should().BeFalse();
    }

    [Fact]
    public void Write_RootedRelativePath_ThrowsAndWritesNothing()
    {
        // Arrange
        string destination = Path.Combine(_tempDirectory, "skill");

        // Act
        Action act = () => _writer.Write(destination,
        [
            new RemoteFile { RelativePath = Path.Combine(_tempDirectory, "escaped.md"), Content = Encoding.UTF8.GetBytes("evil") },
        ]);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        Directory.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public void Write_RelativePathWithTraversalSegment_Throws()
    {
        // Arrange
        string destination = Path.Combine(_tempDirectory, "skill");

        // Act
        Action act = () => _writer.Write(destination,
        [
            new RemoteFile { RelativePath = "../escaped.md", Content = Encoding.UTF8.GetBytes("evil") },
        ]);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion // Write
}
