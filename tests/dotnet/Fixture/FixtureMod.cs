using System.Text.Json;
using F4Forge.Net.Mods;

namespace F4Forge.Tests.Fixture;

public sealed class FixtureMod : IMod
{
    public void Start(ModContext context)
    {
        context.Messages.Register<JsonElement, JsonElement>("fixture.echo", arguments => context.Messages.Call<JsonElement>("native.echo", arguments));
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
