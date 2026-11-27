using F4Forge;

namespace Examples.HelloWorld;

public sealed class HelloWorldPlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        GameEvents.OnGameLoaded += (_, _) => context.Log.Info("Hello world");
    }
}
