using F4Forge.Hosting;

namespace F4Forge.Tests;

public sealed class PluginDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("F4Forge-").FullName;
    private readonly List<string> _errors = [];

    [Theory]
    [InlineData("")]
    [InlineData("Nested/Плагин Ω")]
    public void InitializesPluginFromItsOwnDirectory(string relativeDirectory)
    {
        var directory = DeployFixture(relativeDirectory);
        File.Move(Path.Combine(directory, "Fixture.dll"), Path.Combine(directory, "Renamed.DLL"));
        File.Copy(typeof(IPlugin).Assembly.Location, Path.Combine(directory, "F4Forge.Net.dll"));
        File.WriteAllBytes(Path.Combine(directory, "Unmanaged.dll"), [0x4d, 0x5a]);

        new PluginLoader(_errors.Add).LoadDirectory(_root);

        Assert.Empty(_errors);
        Assert.Equal([directory], File.ReadAllLines(Path.Combine(directory, "initialized.txt")));
    }

    [Fact]
    public void FailedPluginDoesNotPreventOtherPluginsFromInitializing()
    {
        var failed = DeployFixture("A");
        File.WriteAllText(Path.Combine(failed, "fail-start"), "");
        var working = DeployFixture("B");

        new PluginLoader(_errors.Add).LoadDirectory(_root);

        var error = Assert.Single(_errors);
        Assert.Contains(Path.Combine(failed, "Fixture.dll"), error);
        Assert.False(File.Exists(Path.Combine(failed, "initialized.txt")));
        Assert.Equal([working], File.ReadAllLines(Path.Combine(working, "initialized.txt")));
    }

    [Fact]
    public void CreatesMissingPluginDirectory()
    {
        var directory = Path.Combine(_root, "F4ForgeNet");
        new PluginLoader(_errors.Add).LoadDirectory(directory);

        Assert.True(Directory.Exists(directory));
        Assert.Empty(_errors);
    }

    private string DeployFixture(string relativeDirectory)
    {
        var directory = Path.GetFullPath(Path.Combine(_root, relativeDirectory));
        Directory.CreateDirectory(directory);
        foreach (var assembly in new[] { "Fixture.dll", "FixtureDependency.dll" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, assembly), Path.Combine(directory, assembly));
        }
        return directory;
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
