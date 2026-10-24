using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Console;

public sealed partial class GameConsole : IGameSingleton<GameConsole>
{
    static GameConsole IGameSingleton<GameConsole>.Create(ModContext context)
    {
        return new GameConsole(context.Messages);
    }
}
