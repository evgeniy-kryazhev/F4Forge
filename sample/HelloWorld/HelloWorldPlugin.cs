using F4Forge.DotNet.Sdk;

namespace F4Forge.Sample.HelloWorld;

public sealed class HelloWorldPlugin : F4ForgePlugin
{
    public override string Id => "sample.hello_world";

    public override void OnLoad()
    {
        Logger.Info("Hello world from F4Forge!");
    }
}
