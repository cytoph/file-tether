namespace FileTether.Core.Providers.Internal;

internal sealed class ProviderResponseException : Exception
{
    public ProviderResponseException(string message)
        : base(message)
    {
    }

    public ProviderResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
