using System.Globalization;

namespace F4Forge.Tests.BridgeProbe;

public sealed class BridgeProbePlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        var point = Bridge.Invoke<nint>("Test_GetPoint");
        var squaredLength = Bridge.Invoke<float>("NiPoint3_SqrLength", point);
        var dotProduct = AutoBindings.NiPoint3_Dot(point, point);
        if (squaredLength != 25f || dotProduct != 25f)
        {
            throw new InvalidOperationException($"Generated CommonLibF4 bindings returned {squaredLength} and {dotProduct}.");
        }
        // This CommonLib static method compares pointers without accessing the game runtime.
        if (!AutoBindings.BGSInventoryList_StandardObjectCompareCallbackFn(0, 0))
        {
            throw new InvalidOperationException("Generated static binding should consider two null pointers equal.");
        }

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

        CheckCommonLibEvent();
        var resultPath = Path.Combine(context.Directory, "bridge-result.txt");
        File.WriteAllText(resultPath, sum.ToString(CultureInfo.InvariantCulture));
    }

    private static void CheckCommonLibEvent()
    {
        var deliveries = 0;
        AutoEvents.TESObjectLoadedEvent received = default;
        using (Bridge.On<AutoEvents.TESObjectLoadedEvent>(AutoEvents.TESObjectLoadedEvent.Name, (sender, args) =>
        {
            if (sender is not nint address || address == 0)
            {
                throw new InvalidOperationException("Missing native event source.");
            }
            deliveries++;
            received = args;
        }))
        {
            if (!Bridge.Invoke<bool>("Test_EmitObjectLoaded"))
            {
                throw new InvalidOperationException("The generated sink stopped event propagation.");
            }
        }
        Bridge.Invoke<bool>("Test_EmitObjectLoaded");
        if (deliveries != 1 || received.FormID != 0xFEDC1234u || !received.Loaded)
        {
            throw new InvalidOperationException("Incorrect generated event payload or subscription lifetime.");
        }
    }
}
