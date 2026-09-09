using F4Forge.DotNet.Sdk;

namespace F4Forge.PluginFixture;

public sealed class FixturePlugin : F4ForgePlugin
{
    public override string Id => "fixture.plugin";

    public override void OnLoad()
    {
    }

}

public sealed class ZDependentPlugin : F4ForgePlugin
{
    public override string Id => "dependent.plugin";
}
