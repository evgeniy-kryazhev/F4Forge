using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Sample;

public class Modification : F4ForgePlugin
{
    public override string Id => "f4forge.sample";

    public override void OnLoad(F4ForgePluginContext context)
    {
        context.Events.GameDataReady += OnGameDataReady;
        context.Events.GameLoaded += OnGameLoaded;
        context.Events.NewGame += OnNewGame;
    }

    private static void OnGameDataReady(object? sender, EventArgs args) { }
    private static void OnGameLoaded(object? sender, EventArgs args) { }
    private static void OnNewGame(object? sender, EventArgs args) { }
}
