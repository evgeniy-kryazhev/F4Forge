using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Interception;

public sealed partial class GameInterception : IGameSingleton<GameInterception>
{
    static GameInterception IGameSingleton<GameInterception>.Create(ModContext context)
    {
        return new GameInterception(context.Messages, context.CleanupMessages, context.Lifetime);
    }
}
