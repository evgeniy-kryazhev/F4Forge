using F4Forge.Actors;
using F4Forge.Game;
using F4Forge.Input;
using F4Forge.Plugins;

namespace Examples.HelloWorld;

public sealed class HelloWorldPlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        GameEvents.OnGameLoaded += (_, _) => context.Log.Info("Hello world");
        const int keyboardDevice = 0;
        InputEvents.OnButton += (_, button) =>
        {
            if (button.Device == keyboardDevice && button.Value != 0 && button.HeldDownSecs == 0)
            {
                context.Log.Info($"Keyboard key pressed: {button.IdCode}");
            }
        };
    }
}
