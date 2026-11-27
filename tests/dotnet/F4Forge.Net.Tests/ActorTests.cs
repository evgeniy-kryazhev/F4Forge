using F4Forge.Hosting;

namespace F4Forge.Tests;

public sealed class ActorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("F4Forge-Actor-").FullName;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoutesActorDeathToLoadedPlugin(bool failStart)
    {
        foreach (var assembly in new[] { "Fixture.dll", "FixtureDependency.dll" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, assembly), Path.Combine(_directory, assembly));
        }
        File.WriteAllText(Path.Combine(_directory, "death-probe"), "");
        if (failStart)
        {
            File.WriteAllText(Path.Combine(_directory, "fail-start"), "");
        }
        var errors = new List<string>();
        new PluginLoader(errors.Add).LoadDirectory(_directory);
        if (failStart)
        {
            Assert.Contains("Fixture initialization failed", Assert.Single(errors));
        }
        else
        {
            Assert.Empty(errors);
        }

        // These borrowed addresses are used only as identities in this process.
        var name = Events.TESDeathEvent.Name;
        Bridge.Publish(name, null, new Events.TESDeathEvent(0x10, 0x20, false));
        Bridge.Publish(name, null, new Events.TESDeathEvent(0, 0x20, true));
        var marker = Path.Combine(_directory, "deaths.txt");
        Assert.False(File.Exists(marker));
        Bridge.Publish(name, null, new Events.TESDeathEvent(0x30, 0x40, true));
        Bridge.Publish(name, null, new Events.TESDeathEvent(0x50, 0, true));
        if (failStart)
        {
            Assert.False(File.Exists(marker));
        }
        else
        {
            Assert.Equal(["30"], File.ReadAllLines(marker));
        }
    }

    [Fact]
    public void WrapsBorrowedActorByAddress()
    {
        Assert.Null(Actor.FromHandle(0));
        var actor = Assert.IsType<Actor>(Actor.FromHandle(0xBEEF));
        Assert.Equal((nint)0xBEEF, actor.Handle);
        Assert.Equal(actor, Actor.FromHandle(0xBEEF));
        Assert.NotEqual(actor, Actor.FromHandle(0xFEED));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
