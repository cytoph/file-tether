using System.Text.Json;

namespace FileTether.Core.Providers.Internal;

public static class JsonHelpers
{
    public static JsonElement GetRequiredProperty(JsonElement element, string name, string apiName)
    {
        if (element.TryGetProperty(name, out JsonElement value))
        {
            return value;
        }

        throw new ProviderResponseException($"{apiName} response missing expected field '{name}'.");
    }

    public static string GetRequiredStringProperty(JsonElement element, string name, string apiName)
    {
        if (GetRequiredProperty(element, name, apiName).GetString() is { } text)
        {
            return text;
        }

        throw new ProviderResponseException($"{apiName} response field '{name}' was not a string.");
    }
}
