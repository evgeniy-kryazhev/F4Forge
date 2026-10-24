using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Diagnostics;

public sealed partial class GameDiagnostics : IGameSingleton<GameDiagnostics>
{
    static GameDiagnostics IGameSingleton<GameDiagnostics>.Create(ModContext context)
    {
        return new GameDiagnostics(context.ModId, context.Messages);
    }
}
