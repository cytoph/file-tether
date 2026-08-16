using FileTether.Core.Configuration;
using FileTether.Core.Configuration.Models;
using FileTether.Core.State;
using FileTether.Core.State.Models;
using FileTether.Core.Tethering.Internal;
using FileTether.Core.Tethering.Models;

namespace FileTether.Core.Tethering;

public sealed class TetherSession
{
    private readonly ConfigLoader _configLoader;
    private readonly StateStore _stateStore;

    public TetherPaths Paths { get; private set; } = null!;

    public List<TetherEntry> Entries { get; private set; } = [];

    public TetherState State { get; private set; } = new();

    public TetherSession(ConfigLoader configLoader, StateStore stateStore)
    {
        _configLoader = configLoader;
        _stateStore = stateStore;
    }

    public Result Load(string? configOverride)
    {
        TetherPaths paths = TetherPaths.Resolve(configOverride);
        Result<TetherConfig> configResult = _configLoader.Load(paths.ConfigPath, paths.EnvPath);

        if (!configResult.Success)
        {
            return Result.Fail(configResult.Message);
        }

        TetherConfig config = configResult.Value;

        Paths = paths;
        Entries = TetherEntryMapper.Map(config);
        State = _stateStore.Load(paths.StatePath);

        return Result.Ok();
    }

    public void SaveState()
    {
        _stateStore.Save(Paths.StatePath, State);
    }
}
