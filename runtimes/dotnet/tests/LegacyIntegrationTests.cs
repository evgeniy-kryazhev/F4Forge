namespace F4Forge.DotNet.Tests;

public sealed class LegacyIntegrationTests
{
    [Fact]
    public void RuntimeContractScenariosPass()
    {
        Assert.Equal(0, Program.Run());
    }
}
