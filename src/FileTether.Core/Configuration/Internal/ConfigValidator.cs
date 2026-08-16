using System.Text.RegularExpressions;
using FileTether.Core.Configuration.Models;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Configuration.Internal;

public static partial class ConfigValidator
{
    private static readonly StringComparer s_destinationComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex RepoPattern();

    [GeneratedRegex(@"^[0-9a-fA-F]+$")]
    private static partial Regex GistIdPattern();

    public static List<string> Validate(TetherConfig config)
    {
        List<string> errors = [];

        for (int i = 0; i < config.Tethers.Count; i++)
        {
            string entryContext = $"tethers[{i}]";
            errors.AddRange(config.Tethers[i] switch
            {
                GitHubTetherEntryConfig githubTether => ValidateGitHub(githubTether, entryContext),
                GistTetherEntryConfig gistTether => ValidateGist(gistTether, entryContext),
                _ => [],
            });
        }

        List<(string Raw, string Normalized)> destinations = CollectDestinations(config);
        errors.AddRange(FindDuplicateDestinations(destinations));

        if (errors.Count > 0)
        {
            return errors;
        }

        errors.AddRange(FindDuplicateNames(TetherEntryMapper.Map(config)));

        return errors;
    }

    private static string NormalizeDestination(string destination)
    {
        return PathExpander.Expand(destination);
    }

    private static List<string> ValidateGitHub(GitHubTetherEntryConfig tether, string entryContext)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(tether.Repo))
        {
            errors.Add($"{entryContext}: github entries require 'repo' (owner/name).");
        }
        else if (!RepoPattern().IsMatch(tether.Repo))
        {
            errors.Add($"{entryContext}: 'repo' must be in 'owner/name' format.");
        }

        if (tether.Files.Count == 0)
        {
            errors.Add($"{entryContext}: github entries require a non-empty 'files' list.");

            return errors;
        }

        for (int i = 0; i < tether.Files.Count; i++)
        {
            GitHubFileConfig mapping = tether.Files[i];

            if (string.IsNullOrWhiteSpace(mapping.Path))
            {
                errors.Add($"{entryContext}.files[{i}]: missing 'path'.");
            }

            if (string.IsNullOrWhiteSpace(mapping.Destination))
            {
                errors.Add($"{entryContext}.files[{i}]: missing 'destination'.");
            }
        }

        return errors;
    }

    private static List<string> ValidateGist(GistTetherEntryConfig tether, string entryContext)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(tether.Id))
        {
            errors.Add($"{entryContext}: gist entries require 'id'.");
        }
        else if (!GistIdPattern().IsMatch(tether.Id))
        {
            errors.Add($"{entryContext}: 'id' must be hexadecimal.");
        }

        bool hasFlatDestination = !string.IsNullOrWhiteSpace(tether.Destination);
        bool hasFilesList = tether.Files is { Count: > 0 };

        if (hasFlatDestination && hasFilesList)
        {
            errors.Add($"{entryContext}: gist entries must use either 'destination' or 'files', not both.");

            return errors;
        }

        if (!hasFlatDestination && !hasFilesList)
        {
            errors.Add($"{entryContext}: gist entries require 'destination' or a non-empty 'files' list.");

            return errors;
        }

        if (!hasFilesList)
        {
            return errors;
        }

        for (int i = 0; i < tether.Files!.Count; i++)
        {
            GistFileConfig file = tether.Files[i];

            if (string.IsNullOrWhiteSpace(file.Filename))
            {
                errors.Add($"{entryContext}.files[{i}]: missing 'filename'.");
            }

            if (string.IsNullOrWhiteSpace(file.Destination))
            {
                errors.Add($"{entryContext}.files[{i}]: missing 'destination'.");
            }
        }

        return errors;
    }

    private static List<(string Raw, string Normalized)> CollectDestinations(TetherConfig config)
    {
        List<(string Raw, string Normalized)> destinations = [];

        foreach (TetherEntryConfig tether in config.Tethers)
        {
            switch (tether)
            {
                case GitHubTetherEntryConfig githubTether:
                    CollectGitHubDestinations(githubTether, destinations);

                    break;
                case GistTetherEntryConfig gistTether:
                    CollectGistDestinations(gistTether, destinations);

                    break;
            }
        }

        return destinations;
    }

    private static void CollectGitHubDestinations(GitHubTetherEntryConfig tether, List<(string Raw, string Normalized)> destinations)
    {
        foreach (GitHubFileConfig mapping in tether.Files)
        {
            if (!string.IsNullOrWhiteSpace(mapping.Destination))
            {
                destinations.Add((mapping.Destination, NormalizeDestination(mapping.Destination)));
            }
        }
    }

    private static void CollectGistDestinations(GistTetherEntryConfig tether, List<(string Raw, string Normalized)> destinations)
    {
        if (!string.IsNullOrWhiteSpace(tether.Destination))
        {
            destinations.Add((tether.Destination, NormalizeDestination(tether.Destination)));
        }

        if (tether.Files is null)
        {
            return;
        }

        foreach (GistFileConfig file in tether.Files)
        {
            if (!string.IsNullOrWhiteSpace(file.Destination))
            {
                destinations.Add((file.Destination, NormalizeDestination(file.Destination)));
            }
        }
    }

    private static List<string> FindDuplicateDestinations(List<(string Raw, string Normalized)> destinations)
    {
        List<string> errors = [];
        IEnumerable<IGrouping<string, (string Raw, string Normalized)>> duplicateGroups = destinations
            .GroupBy(destination => destination.Normalized, s_destinationComparer)
            .Where(group => group.Skip(1).Any());

        foreach (IGrouping<string, (string Raw, string Normalized)> group in duplicateGroups)
        {
            errors.Add($"Duplicate destination '{group.First().Raw}' across tether entries.");
        }

        return errors;
    }

    private static List<string> FindDuplicateNames(List<TetherEntry> entries)
    {
        List<string> errors = [];
        IEnumerable<IGrouping<string, string>> duplicateGroups = entries
            .Select(entry => entry.DisplayName)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Skip(1).Any());

        foreach (IGrouping<string, string> group in duplicateGroups)
        {
            errors.Add($"Duplicate name '{group.Key}' across tether entries.");
        }

        return errors;
    }
}
