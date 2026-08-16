using FileTether.Core.Configuration.Models;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Tethering.Internal;

public static class TetherEntryMapper
{
    public static List<TetherEntry> Map(TetherConfig config)
    {
        List<TetherEntry> entries = [];

        foreach (TetherEntryConfig tether in config.Tethers)
        {
            switch (tether)
            {
                case GitHubTetherEntryConfig githubTether:
                    entries.AddRange(MapGitHub(githubTether));

                    break;
                case GistTetherEntryConfig gistTether:
                    entries.AddRange(MapGist(gistTether));

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported tether config type '{tether.GetType().Name}'.");
            }
        }

        return entries;
    }

    private static List<TetherEntry> MapGitHub(GitHubTetherEntryConfig tether)
    {
        List<TetherEntry> entries = [];

        foreach (GitHubFileConfig mapping in tether.Files)
        {
            entries.Add(new GitHubTetherEntry
            {
                Token = tether.Token,
                Name = mapping.Name,
                Repo = tether.Repo,
                Path = mapping.Path,
                Destination = PathExpander.Expand(mapping.Destination),
            });
        }

        return entries;
    }

    private static List<TetherEntry> MapGist(GistTetherEntryConfig tether)
    {
        if (tether.Files is { Count: > 0 })
        {
            return MapGistFiles(tether);
        }

        return [MapGistFlat(tether)];
    }

    private static List<TetherEntry> MapGistFiles(GistTetherEntryConfig tether)
    {
        List<TetherEntry> entries = [];

        foreach (GistFileConfig file in tether.Files!)
        {
            entries.Add(new GistTetherEntry
            {
                Token = tether.Token,
                Name = file.Name,
                GistId = tether.Id,
                Filename = file.Filename,
                Destination = PathExpander.Expand(file.Destination),
            });
        }

        return entries;
    }

    private static GistTetherEntry MapGistFlat(GistTetherEntryConfig tether)
    {
        return new GistTetherEntry
        {
            Token = tether.Token,
            Name = tether.Name,
            GistId = tether.Id,
            Filename = tether.Filename,
            Destination = PathExpander.Expand(tether.Destination),
        };
    }
}
