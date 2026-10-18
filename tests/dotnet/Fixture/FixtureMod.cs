using System.Text.Json;
using F4Forge.Net.Mods;
using F4Forge.Tests.FixtureDependency;

namespace F4Forge.Tests.Fixture;

public sealed class FixtureMod : IMod
{
    public void Start(ModContext context)
    {
        context.Messages.Register<JsonElement, JsonElement>("fixture.echo", arguments => FixtureValues.Identity(context.Messages.Call<JsonElement>("native.echo", arguments)));
        context.Messages.Register<object?, string>("fixture.directory", _ => context.Directory);
        context.Events.Subscribe<JsonElement>("fixture.input", data => context.Events.Publish("fixture.output", data));
        if (File.Exists(Path.Combine(context.Directory, "fail-start")))
        {
            throw new InvalidOperationException("Fixture startup failed after registration.");
        }
    }

    public void Dispose()
    {
    }
}
