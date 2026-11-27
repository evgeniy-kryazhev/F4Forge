using System.Globalization;

namespace F4Forge.Tests.BridgeProbe;

using F4Forge.Bridge;
using F4Forge.Input;
using F4Forge.Plugins;

public sealed class BridgeProbePlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        var point = Bridge.Invoke<nint>("Test_GetPoint");
        var squaredLength = Bridge.Invoke<float>("NiPoint3.SqrLength", point);
        var dotProduct = Bindings.NiPoint3.Dot(point, point);
        if (squaredLength != 25f || dotProduct != 25f)
        {
            throw new InvalidOperationException($"Generated CommonLibF4 bindings returned {squaredLength} and {dotProduct}.");
        }
        // This CommonLib static method compares pointers without accessing the game runtime.
        if (!Bindings.BGSInventoryList.StandardObjectCompareCallbackFn(0, 0))
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
            context.Log.Info(args);
        });
        var delivered = Bridge.Invoke<int>("Test_EmitMessage");
        if (delivered != 1 || receivedMessage != "Привет из F4Forge")
        {
            throw new InvalidOperationException("The plugin did not receive the native UTF-8 event.");
        }

        CheckCommonLibEvent();
        CheckInputEvents();
        var resultPath = Path.Combine(context.Directory, "bridge-result.txt");
        File.WriteAllText(resultPath, sum.ToString(CultureInfo.InvariantCulture));
    }

    private static void CheckCommonLibEvent()
    {
        var deliveries = 0;
        Events.TESObjectLoadedEvent received = default;
        using (Bridge.On<Events.TESObjectLoadedEvent>(Events.TESObjectLoadedEvent.Name, (sender, args) =>
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

    private static void CheckInputEvents()
    {
        var buttons = new List<Events.ButtonEvent>();
        var characters = new List<uint>();
        var movements = new List<(int, int)>();
        var positions = new List<(int, int)>();
        void Button(object? sender, Events.ButtonEvent args) => buttons.Add(args);
        void Character(object? sender, Events.CharacterEvent args) => characters.Add(args.CharCode);
        void MouseMove(object? sender, Events.MouseMoveEvent args) => movements.Add((args.MouseInputX, args.MouseInputY));
        void CursorMove(object? sender, Events.CursorMoveEvent args) => positions.Add((args.CursorPosX, args.CursorPosY));

        InputEvents.OnButton += Button;
        InputEvents.OnCharacter += Character;
        InputEvents.OnMouseMove += MouseMove;
        InputEvents.OnCursorMove += CursorMove;
        try
        {
            if (!Bridge.Invoke<bool>("Test_EmitInput"))
            {
                throw new InvalidOperationException("The generated input handler accepted the wrong event types.");
            }
        }
        finally
        {
            InputEvents.OnButton -= Button;
            InputEvents.OnCharacter -= Character;
            InputEvents.OnMouseMove -= MouseMove;
            InputEvents.OnCursorMove -= CursorMove;
        }
        Bridge.Invoke<bool>("Test_EmitInput");
        if (buttons is not [{ Device: 0, IdCode: 30, Value: 1f, HeldDownSecs: 0f },
                            { Device: 1, IdCode: 1, Value: 0f, HeldDownSecs: 0.25f }]
            || characters is not [0x0416u] || movements is not [(-7, 3)] || positions is not [(320, 240)])
        {
            throw new InvalidOperationException("Incorrect input snapshots, delivery count, or subscription lifetime.");
        }
    }
}
