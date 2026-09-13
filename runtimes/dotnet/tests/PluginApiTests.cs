using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed class PluginApiTests
{
    [Fact]
    public void PluginExposesIdentityAndLoadHook()
    {
        var plugin = new TestPlugin();

        plugin.OnLoad();

        Assert.Equal("test.plugin", plugin.Id);
        Assert.True(plugin.Loaded);
    }

    private sealed class TestPlugin : F4ForgePlugin
    {
        public bool Loaded { get; private set; }

        public override string Id => "test.plugin";

        public override void OnLoad() => Loaded = true;
    }
}
