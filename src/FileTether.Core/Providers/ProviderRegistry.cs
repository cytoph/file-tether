namespace FileTether.Core.Providers;

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IFileProvider> _providers;

    public IReadOnlyCollection<string> Names => _providers.Keys;

    public ProviderRegistry(IEnumerable<IFileProvider> providers)
    {
        _providers = providers.ToDictionary(provider => provider.Name);
    }

    public IFileProvider Resolve(string providerType)
    {
        if (_providers.TryGetValue(providerType, out IFileProvider? provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Unknown provider type '{providerType}'. Known providers: {string.Join(", ", Names)}.");
    }
}
