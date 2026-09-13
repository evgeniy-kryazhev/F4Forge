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

    [Fact]
    public void ContextExposesSpecializedServices()
    {
        var context = new F4ForgePluginContext(default, _ => { }, null, CancellationToken.None);

        Assert.NotNull(context.Events);
        Assert.NotNull(context.Input);
        Assert.NotNull(context.Endpoints);
        Assert.NotNull(context.Capabilities);
        Assert.NotNull(context.GameThread);
        Assert.Equal(default, context.Endpoints.Resolve("missing.endpoint"));
        Assert.Equal(0u, context.Capabilities.Query("missing.capability"));
    }

    private sealed class TestPlugin : F4ForgePlugin
    {
        public bool Loaded { get; private set; }

        public override string Id => "test.plugin";

        public override void OnLoad() => Loaded = true;
    }
}
