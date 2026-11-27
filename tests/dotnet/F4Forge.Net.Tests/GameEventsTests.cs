namespace F4Forge.Tests;

public sealed class GameEventsTests
{
    [Fact]
    public void LoadNotificationsForwardTheirGeneratedPayloads()
    {
        var beforeCompletion = 0;
        var cells = new List<nint>();
        var objects = new List<Events.TESObjectLoadedEvent>();
        void OnBeforeCompletion(object? sender, Events.DoBeforeNewOrLoadCompletedEvent args) => beforeCompletion++;
        void OnCellLoaded(object? sender, Events.TESCellFullyLoadedEvent args) => cells.Add(args.Cell);
        void OnObjectChanged(object? sender, Events.TESObjectLoadedEvent args) => objects.Add(args);

        var cell = new Events.TESCellFullyLoadedEvent(0x1234);
        var loaded = new Events.TESObjectLoadedEvent(0xFE001234u, true);
        var unloaded = loaded with { Loaded = false };
        GameEvents.OnBeforeNewOrLoadCompleted += OnBeforeCompletion;
        GameEvents.OnCellFullyLoaded += OnCellLoaded;
        GameEvents.OnObjectLoadChanged += OnObjectChanged;
        try
        {
            Bridge.Publish(Events.DoBeforeNewOrLoadCompletedEvent.Name, null, new Events.DoBeforeNewOrLoadCompletedEvent());
            Bridge.Publish(Events.TESCellFullyLoadedEvent.Name, null, cell);
            Bridge.Publish(Events.TESObjectLoadedEvent.Name, null, loaded);
            Bridge.Publish(Events.TESObjectLoadedEvent.Name, null, unloaded);
        }
        finally
        {
            GameEvents.OnBeforeNewOrLoadCompleted -= OnBeforeCompletion;
            GameEvents.OnCellFullyLoaded -= OnCellLoaded;
            GameEvents.OnObjectLoadChanged -= OnObjectChanged;
        }
        Bridge.Publish(Events.DoBeforeNewOrLoadCompletedEvent.Name, null, new Events.DoBeforeNewOrLoadCompletedEvent());
        Bridge.Publish(Events.TESCellFullyLoadedEvent.Name, null, cell);
        Bridge.Publish(Events.TESObjectLoadedEvent.Name, null, loaded);
        Assert.Equal(1, beforeCompletion);
        Assert.Equal(cell.Cell, Assert.Single(cells));
        Assert.Equal([loaded, unloaded], objects);
    }

    [Fact]
    public void GameLoadedForwardsTheGeneratedEventAndRemovesOnlyItsOwnSubscription()
    {
        var count = 0;
        var source = new object();
        void OnLoaded(object? sender, Events.TESLoadGameEvent args)
        {
            Assert.Same(source, sender);
            count++;
        }

        GameEvents.OnGameLoaded += OnLoaded;
        using var direct = Bridge.On<Events.TESLoadGameEvent>(Events.TESLoadGameEvent.Name, OnLoaded);
        try
        {
            Bridge.Publish(Events.TESLoadGameEvent.Name, source, new Events.TESLoadGameEvent());
            Assert.Equal(2, count);
        }
        finally
        {
            GameEvents.OnGameLoaded -= OnLoaded;
        }
        Bridge.Publish(Events.TESLoadGameEvent.Name, source, new Events.TESLoadGameEvent());
        Assert.Equal(3, count);
        direct.Dispose();
        Bridge.Publish(Events.TESLoadGameEvent.Name, source, new Events.TESLoadGameEvent());
        Assert.Equal(3, count);
    }
}
