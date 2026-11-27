using F4Forge.Net.Mods;
using F4Forge.Tests.FixtureDependency;

namespace F4Forge.Tests.Fixture;

public sealed class FixtureMod : IPlugin
{
    public void Start(PluginContext context)
    {
        var directory = context.Directory;
        if (File.Exists(Path.Combine(directory, "fail-start")))
        {
            throw new InvalidOperationException("Fixture initialization failed.");
        }

        var marker = Path.Combine(directory, "initialized.txt");
        File.AppendAllLines(marker, [FixtureValues.Identity(directory)]);
    }
}
