using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Papyrus;

public sealed partial class GamePapyrus : IGameSingleton<GamePapyrus>
{
    static GamePapyrus IGameSingleton<GamePapyrus>.Create(ModContext context)
    {
        return new GamePapyrus(context.Messages, context.CleanupMessages, context.Events, context.Lifetime);
    }
}
