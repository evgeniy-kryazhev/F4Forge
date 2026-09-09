using System.Text.Json;
using F4Forge.DotNet.Sdk;

namespace F4Forge.Sample.HelloWorld;

public sealed class HelloWorldPlugin : F4ForgePlugin
{
    public override string Id => "mod.hello_world";

    public override void OnLoad(F4ForgePluginContext context)
    {
        Logger.Info("Hello world from F4Forge!");

        context.Events.KeyDown += args =>
        {
            if (!args.IsMenu)
            {
                var json = JsonSerializer.Serialize(args);
                Logger.Info($"Some event triggered! {json}, menu={args.IsMenu}");
            }
        };
    }
}
