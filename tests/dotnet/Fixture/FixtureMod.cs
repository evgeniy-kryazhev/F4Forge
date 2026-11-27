using F4Forge.Net.Mods;
using F4Forge.Tests.FixtureDependency;

namespace F4Forge.Tests.Fixture;

public sealed class FixtureMod : IMod
{
    public void Initialize(string directory)
    {
        if (File.Exists(Path.Combine(directory, "fail-start")))
        {
            throw new InvalidOperationException("Fixture initialization failed.");
        }

        var marker = Path.Combine(directory, "initialized.txt");
        File.AppendAllLines(marker, [FixtureValues.Identity(directory)]);
    }
}
