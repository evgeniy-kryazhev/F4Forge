using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Sample;

public class Modification : F4ForgePlugin
{
    public override void OnLoad(F4ForgePluginContext context)
    {
        context.Events.GameDataReady += OnGameDataReady;
        context.Events.GameLoaded += OnGameLoaded;
        context.Events.NewGame += OnNewGame;
    }

    private static void OnGameDataReady() { }
    private static void OnGameLoaded() { }
    private static void OnNewGame() { }
}
