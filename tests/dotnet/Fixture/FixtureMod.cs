using System.Text.Json;
using F4Forge.Net.Mods;
using F4Forge.Net.Operations;
using F4Forge.Tests.FixtureDependency;

namespace F4Forge.Tests.Fixture;

public sealed class FixtureMod : IMod
{
    private string? _directory;

    public async Task StartAsync(ModContext context, CancellationToken cancellationToken)
    {
        _directory = context.Directory;
        if (File.Exists(Path.Combine(context.Directory, "async-start")))
        {
            context.Messages.Register<object?, string>("fixture.starting", _ => context.Directory);
            while (!File.Exists(Path.Combine(context.Directory, "release-start")))
            {
                var token = File.Exists(Path.Combine(context.Directory, "ignore-cancellation"))
                    ? CancellationToken.None : cancellationToken;
                await Task.Delay(10, token).ConfigureAwait(false);
            }
            if (File.Exists(Path.Combine(context.Directory, "fail-after-await")))
            {
                throw new InvalidOperationException("Fixture failed after asynchronous startup.");
            }
            return;
        }
        context.Messages.Register<JsonElement, JsonElement>("fixture.echo", arguments =>
        {
            var response = context.Messages.Query(new Echo(arguments));
            return FixtureValues.Identity(response);
        });
        context.Messages.Register<object?, string>("fixture.directory", _ => context.Directory);
        context.Events.Subscribe<JsonElement>("fixture.input", data => context.Events.Publish("fixture.output", data));
        if (File.Exists(Path.Combine(context.Directory, "fail-start")))
        {
            throw new InvalidOperationException("Fixture startup failed after registration.");
        }
    }

    private sealed record Echo(JsonElement Data) : IQuery<JsonElement>
    {
        public string MethodName => "native.echo";
        public object Arguments => Data;
    }

    public async ValueTask DisposeAsync()
    {
        if (_directory is not null && File.Exists(Path.Combine(_directory, "async-start")))
        {
            await Task.Yield();
            await File.AppendAllTextAsync(Path.Combine(_directory, "disposed"), "disposed\n").ConfigureAwait(false);
        }
    }
}
