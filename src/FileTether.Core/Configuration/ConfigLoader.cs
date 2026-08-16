using FileTether.Core.Configuration.Internal;
using FileTether.Core.Configuration.Models;
using Microsoft.Extensions.Logging;

namespace FileTether.Core.Configuration;

public sealed class ConfigLoader
{
    private readonly EnvFileLoader _envFileLoader;
    private readonly ILogger<ConfigLoader> _logger;

    public ConfigLoader(EnvFileLoader envFileLoader, ILogger<ConfigLoader> logger)
    {
        _envFileLoader = envFileLoader;
        _logger = logger;
    }

    public Result<TetherConfig> Load(string configPath, string envPath)
    {
        if (!File.Exists(configPath))
        {
            return Result.Fail<TetherConfig>($"Config file not found: {configPath}. Run 'file-tether init' to scaffold one.");
        }

        Result<TetherConfig> parseResult = ConfigParser.Parse(File.ReadAllText(configPath));

        if (!parseResult.Success)
        {
            return Result.Fail<TetherConfig>(parseResult.Message);
        }

        TetherConfig config = parseResult.Value;
        VariableResolver resolver = new(_envFileLoader.Load(envPath));

        List<string> errors = resolver.ResolveAll(config);
        errors.AddRange(ConfigValidator.Validate(config));

        if (errors.Count > 0)
        {
            string details = string.Join(Environment.NewLine, errors.Select(error => $"  - {error}"));

            return Result.Fail<TetherConfig>($"Config errors:{Environment.NewLine}{details}");
        }

        _logger.LogDebug("Loaded config from {Path} with {Count} tethers", configPath, config.Tethers.Count);

        return config;
    }
}
