using System.Text.Json;
using FileTether.Core.State.Models;
using Microsoft.Extensions.Logging;

namespace FileTether.Core.State;

public sealed class StateStore
{
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ILogger<StateStore> _logger;

    public StateStore(ILogger<StateStore> logger)
    {
        _logger = logger;
    }

    public TetherState Load(string path)
    {
        if (!File.Exists(path))
        {
            return new TetherState();
        }

        TetherState? state;
        try
        {
            state = JsonSerializer.Deserialize<TetherState>(File.ReadAllText(path), s_serializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"State file '{path}' is corrupt or not valid JSON: {exception.Message}. Fix it by hand or delete the file to reset tether state.",
                exception);
        }

        if (state is null)
        {
            return new TetherState();
        }

        foreach (StateEntry entry in state.Entries.Values)
        {
            entry.Fingerprints ??= new Dictionary<string, string>();
        }

        return state;
    }

    public void Save(string path, TetherState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(state, s_serializerOptions));

        _logger.LogDebug("Saved state to {Path}", path);
    }
}
