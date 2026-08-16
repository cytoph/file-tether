using FileTether.Core.Configuration.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FileTether.Core.Configuration.Internal;

public static class ConfigParser
{
    private static readonly IDeserializer s_deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .WithTypeDiscriminatingNodeDeserializer(options =>
        {
            options.AddKeyValueTypeDiscriminator<TetherEntryConfig>("type", new Dictionary<string, Type>
            {
                ["github"] = typeof(GitHubTetherEntryConfig),
                ["gist"] = typeof(GistTetherEntryConfig),
            });
        })
        .Build();

    public static Result<TetherConfig> Parse(string yaml)
    {
        try
        {
            TetherConfig? config = s_deserializer.Deserialize<TetherConfig>(yaml);

            return config ?? new TetherConfig();
        }
        catch (YamlException exception) when (exception.InnerException is InvalidOperationException { InnerException: MissingMethodException })
        {
            return Result.Fail<TetherConfig>("Invalid tether entry in tether.yml: unrecognized or missing 'type'. Each tether entry needs a 'type' of either 'github' or 'gist'.");
        }
        catch (YamlException exception)
        {
            return Result.Fail<TetherConfig>($"Invalid YAML in tether.yml: {exception.Message}");
        }
    }
}
