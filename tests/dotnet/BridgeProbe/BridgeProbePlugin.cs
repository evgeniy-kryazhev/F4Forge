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

        string? receivedMessage = null;
        using var subscription = Bridge.On<string>("Test_Message", (sender, args) =>
        {
            if (sender is not null)
            {
                throw new InvalidOperationException("Expected a native event without a sender.");
            }
            receivedMessage = args;
        });
        var delivered = Bridge.Invoke<int>("Test_EmitMessage");
        if (delivered != 1 || receivedMessage != "Привет из F4Forge")
        {
            throw new InvalidOperationException("The plugin did not receive the native UTF-8 event.");
        }

        var resultPath = Path.Combine(context.Directory, "bridge-result.txt");
        File.WriteAllText(resultPath, sum.ToString(CultureInfo.InvariantCulture));
    }
}
