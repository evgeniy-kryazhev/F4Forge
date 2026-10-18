using System.Runtime.InteropServices;
using F4Forge.Net.Hosting;
using F4Forge.Net.Interop;

namespace F4Forge.Net.Tests;

public sealed partial class ModDiscoveryTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());
    private readonly string _root = Path.Combine(Path.GetTempPath(), "F4ForgeTests", Guid.NewGuid().ToString("N"), "F4ForgeNet");

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData("")]
    [InlineData("Nested/AnotherLevel")]
    public void DiscoversDllWithoutManifestAndResolvesAdjacentDependency(string relativeDirectory)
    {
        var directory = DeployFixture(relativeDirectory);
        // Assembly discovery must not depend on the filename matching its folder or assembly name.
        File.Move(Path.Combine(directory, "Fixture.dll"), Path.Combine(directory, "AnyModName.DLL"));
        var errors = new List<string>();
        using var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Add);

        loader.LoadDirectory(_root);

        Assert.Empty(errors);
        Assert.Equal(1, loader.Count);
        Assert.Equal(directory, _channel.Call<string>("fixture.directory", null));
        Assert.Equal("Dependency loaded Ω", _channel.Call<string>("fixture.echo", "Dependency loaded Ω"));
        Assert.Empty(Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void SkipsDependenciesSdkCopiesAndNativeDlls()
    {
        var directory = DeployFixture("");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "F4Forge.Net.dll"),
            Path.Combine(directory, "F4Forge.Net.dll"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "F4Forge.TestTransport.dll"),
            Path.Combine(directory, "NativeDependency.dll"));
        var errors = new List<string>();
        using var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Add);

        loader.LoadDirectory(_root);

        Assert.Equal(1, loader.Count);
        Assert.Empty(errors);
        Assert.Equal("Still works", _channel.Call<string>("fixture.echo", "Still works"));
    }

    [Fact]
    public void FailedModDoesNotPreventLoadingAnotherDll()
    {
        var failedDirectory = DeployFixture("A-Failed");
        File.WriteAllText(Path.Combine(failedDirectory, "fail-start"), "");
        var workingDirectory = DeployFixture("B-Working/Nested");
        var errors = new List<string>();
        using var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Add);

        loader.LoadDirectory(_root);

        Assert.Equal(1, loader.Count);
        Assert.Single(errors);
        Assert.Contains(Path.Combine(failedDirectory, "Fixture.dll"), errors[0]);
        Assert.Equal(workingDirectory, _channel.Call<string>("fixture.directory", null));
    }

    [Fact]
    public void CreatesMissingModDirectory()
    {
        var errors = new List<string>();
        using var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Add);

        loader.LoadDirectory(_root);

        Assert.True(Directory.Exists(_root));
        Assert.Equal(0, loader.Count);
        Assert.Empty(errors);
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
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);
            }
        }
        finally
        {
            Destroy();
        }
    }
}
