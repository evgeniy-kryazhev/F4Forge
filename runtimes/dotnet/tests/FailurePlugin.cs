using F4Forge.DotNet.Sdk;

namespace F4Forge.FailureFixture;

public sealed class FailurePlugin : F4ForgePlugin
{
    public override string Id => "failure.plugin";
    public override void OnLoad() => throw new InvalidOperationException("load failure");
}
