using System.Text.RegularExpressions;
using FileTether.Core.Configuration.Models;

namespace FileTether.Core.Configuration.Internal;

public sealed partial class VariableResolver
{
    private readonly IReadOnlyDictionary<string, string> _envFileVariables;

    public VariableResolver(IReadOnlyDictionary<string, string> envFileVariables)
    {
        _envFileVariables = envFileVariables;
    }

    public List<string> ResolveAll(TetherConfig config)
    {
        List<string> errors = [];

        for (int i = 0; i < config.Tethers.Count; i++)
        {
            ResolveTether(config.Tethers[i], $"tethers[{i}]", errors);
        }

        return errors;
    }

    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex VariablePattern();

    private void ResolveTether(TetherEntryConfig tether, string entryContext, List<string> errors)
    {
        switch (tether)
        {
            case GitHubTetherEntryConfig githubTether:
                ResolveGitHub(githubTether, entryContext, errors);

                break;
            case GistTetherEntryConfig gistTether:
                ResolveGist(gistTether, entryContext, errors);

                break;
        }
    }

    private void ResolveGitHub(GitHubTetherEntryConfig tether, string entryContext, List<string> errors)
    {
        tether.Token = Resolve(tether.Token, entryContext, errors);
        tether.Repo = Resolve(tether.Repo, entryContext, errors) ?? "";

        foreach (GitHubFileConfig mapping in tether.Files)
        {
            mapping.Path = Resolve(mapping.Path, entryContext, errors) ?? "";
            mapping.Destination = Resolve(mapping.Destination, entryContext, errors) ?? "";
        }
    }

    private void ResolveGist(GistTetherEntryConfig tether, string entryContext, List<string> errors)
    {
        tether.Token = Resolve(tether.Token, entryContext, errors);
        tether.Id = Resolve(tether.Id, entryContext, errors) ?? "";
        tether.Filename = Resolve(tether.Filename, entryContext, errors);
        tether.Destination = Resolve(tether.Destination, entryContext, errors) ?? "";

        if (tether.Files is null)
        {
            return;
        }

        foreach (GistFileConfig file in tether.Files)
        {
            file.Filename = Resolve(file.Filename, entryContext, errors) ?? "";
            file.Destination = Resolve(file.Destination, entryContext, errors) ?? "";
        }
    }

    private string? Resolve(string? value, string entryContext, List<string> errors)
    {
        if (value is null)
        {
            return null;
        }

        return VariablePattern().Replace(value, match =>
        {
            string variableName = match.Groups[1].Value;

            if (_envFileVariables.TryGetValue(variableName, out string? fromEnvFile))
            {
                return fromEnvFile;
            }

            string? fromEnvironment = Environment.GetEnvironmentVariable(variableName);

            if (fromEnvironment is not null)
            {
                return fromEnvironment;
            }

            errors.Add($"Unresolvable variable '${{{variableName}}}' in {entryContext}.");

            return match.Value;
        });
    }
}
