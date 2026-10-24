using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Objects;

public sealed partial class GameObjects : IGameSingleton<GameObjects>
{
    static GameObjects IGameSingleton<GameObjects>.Create(ModContext context)
    {
        return new GameObjects(context.Messages);
    }
}
