using FileTether.Core;
using FileTether.Core.Tethering.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileTether.Core.Tests.Tethering;

public sealed class LocalFingerprintCalculatorTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly LocalFingerprintCalculator _calculator = new(NullLogger<LocalFingerprintCalculator>.Instance);

    public LocalFingerprintCalculatorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    #region Compute

    [Fact]
    public void Compute_PlainFile_UsesEmptyKey()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "a.md");
        File.WriteAllText(filePath, "content");

        // Act
        Dictionary<string, string> fingerprints = _calculator.Compute(filePath);

        // Assert
        fingerprints.Should().ContainSingle();
        fingerprints[""].Should().Be(Hashing.Sha256Hex("content"));
    }

    [Fact]
    public void Compute_Folder_UsesForwardSlashRelativePaths()
    {
        // Arrange
        string folder = Path.Combine(_tempDirectory, "skill");
        Directory.CreateDirectory(Path.Combine(folder, "ref"));
        File.WriteAllText(Path.Combine(folder, "SKILL.md"), "skill");
        File.WriteAllText(Path.Combine(folder, "ref", "notes.md"), "notes");

        // Act
        Dictionary<string, string> fingerprints = _calculator.Compute(folder);

        // Assert
        fingerprints.Should().HaveCount(2);
        fingerprints["SKILL.md"].Should().Be(Hashing.Sha256Hex("skill"));
        fingerprints["ref/notes.md"].Should().Be(Hashing.Sha256Hex("notes"));
    }

    [Fact]
    public void Compute_MissingDestination_ReturnsEmpty()
    {
        // Act
        Dictionary<string, string> fingerprints = _calculator.Compute(Path.Combine(_tempDirectory, "missing"));

        // Assert
        fingerprints.Should().BeEmpty();
    }

    #endregion // Compute
}
