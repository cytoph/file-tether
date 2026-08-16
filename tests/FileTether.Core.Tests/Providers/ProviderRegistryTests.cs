using FileTether.Core;
using FileTether.Core.Providers;
using NSubstitute;

namespace FileTether.Core.Tests.Providers;

public class ProviderRegistryTests
{
    private static IFileProvider ProviderNamed(string name)
    {
        IFileProvider provider = Substitute.For<IFileProvider>();
        provider.Name.Returns(name);

        return provider;
    }

    #region Resolve

    [Fact]
    public void Resolve_ReturnsProviderByName()
    {
        // Arrange
        IFileProvider github = ProviderNamed(GitHubProvider.ProviderName);
        ProviderRegistry registry = new([github, ProviderNamed(GistProvider.ProviderName)]);

        // Act
        IFileProvider resolved = registry.Resolve(GitHubProvider.ProviderName);

        // Assert
        resolved.Should().BeSameAs(github);
    }

    [Fact]
    public void Resolve_UnknownType_ThrowsListingKnownProviders()
    {
        // Arrange
        ProviderRegistry registry = new([ProviderNamed(GitHubProvider.ProviderName), ProviderNamed(GistProvider.ProviderName)]);

        // Act
        Action act = () => registry.Resolve("svn");

        // Assert
        InvalidOperationException exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("svn");
        exception.Message.Should().Contain(GitHubProvider.ProviderName);
        exception.Message.Should().Contain(GistProvider.ProviderName);
    }

    #endregion // Resolve
}
