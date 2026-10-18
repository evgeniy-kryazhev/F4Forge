using System.Text.Json;
using F4Forge.Net.Mods;

namespace F4Forge.Tests.Fixture;

public sealed class FixtureMod : IMod
{
    public void Start(ModContext context)
    {
        context.Messages.Register("fixture.echo", arguments => context.Messages.Call<JsonElement>("native.echo", arguments));
        if (File.Exists(Path.Combine(context.Directory, "fail-start")))
        {
            throw new InvalidOperationException("Fixture startup failed after registration.");
        }
    }

    public void Dispose()
    {
    }
}
