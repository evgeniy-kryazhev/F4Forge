using F4Forge.Tests.FixtureDependency;

namespace F4Forge.Tests.Fixture;

using F4Forge.Actors;
using F4Forge.Bridge;
using F4Forge.Plugins;

public sealed class FixturePlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        var directory = context.Directory;
        if (File.Exists(Path.Combine(directory, "death-probe")))
        {
            void OnDeath(Actor actor)
            {
                File.AppendAllLines(Path.Combine(directory, "deaths.txt"), [actor.Handle.ToString("X")]);
                ActorEvents.OnDeath -= OnDeath;
            }
            ActorEvents.OnDeath += OnDeath;
        }
        if (File.Exists(Path.Combine(directory, "fail-start")))
        {
            Bridge.On<int>("FailedPluginProbe", (_, _) =>
            {
                throw new InvalidOperationException("A failed plugin received an event after unloading.");
            });
            throw new InvalidOperationException("Fixture initialization failed.");
        }

        var marker = Path.Combine(directory, "initialized.txt");
        File.AppendAllLines(marker, [FixtureValues.Identity(directory)]);
    }
}
