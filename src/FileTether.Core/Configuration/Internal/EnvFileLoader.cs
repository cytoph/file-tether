using Microsoft.Extensions.Logging;

namespace FileTether.Core.Configuration.Internal;

public sealed class EnvFileLoader
{
    private readonly ILogger<EnvFileLoader> _logger;

    public EnvFileLoader(ILogger<EnvFileLoader> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, string> Load(string path)
    {
        Dictionary<string, string> variables = new();

        if (!File.Exists(path))
        {
            return variables;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            string trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            int separatorIndex = trimmed.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = trimmed[..separatorIndex].Trim();
            string value = trimmed[(separatorIndex + 1)..].Trim().Trim('"');
            variables[key] = value;
        }

        _logger.LogDebug("Loaded {Count} variable(s) from {Path}", variables.Count, path);

        return variables;
    }
}
