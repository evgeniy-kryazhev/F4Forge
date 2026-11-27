using System.Globalization;

namespace F4Forge.Tests.BridgeProbe;

public sealed class BridgeProbePlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        var sum = Bridge.Invoke<int>("Test_Add", 2, 3);
        if (sum != 5)
        {
            throw new InvalidOperationException($"Test_Add(2, 3) returned {sum}, expected 5.");
        }

        var resultPath = Path.Combine(context.Directory, "bridge-result.txt");
        File.WriteAllText(resultPath, sum.ToString(CultureInfo.InvariantCulture));
    }
}
