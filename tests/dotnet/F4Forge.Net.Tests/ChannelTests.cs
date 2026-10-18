using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Hosting;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

namespace F4Forge.Net.Tests;

public sealed partial class ChannelTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Fact]
    public void NativeRoundTripPreservesUnicode()
    {
        var response = _channel.Call<JsonElement>("native.echo", new { text = "Привет 🌍" });
        Assert.Equal("Привет 🌍", response.GetProperty("text").GetString());
    }

    [Fact]
    public void ManagedHandlerCanCallNativeHandler()
    {
        using var registration = _channel.Register<JsonElement, JsonElement>("managed.echo", arguments => _channel.Call<JsonElement>("native.echo", arguments));
        Assert.Equal(42, _channel.Call<int>("managed.echo", 42));
    }

    [Fact]
    public void DisposalRemovesHandlerAndIsIdempotent()
    {
        var registration = _channel.Register<object?, bool>("managed.temporary", _ => true);
        registration.Dispose();
        registration.Dispose();
        var exception = Assert.Throws<MessageException>(() => _channel.Call<bool>("managed.temporary", null));
        Assert.Equal("method_not_found", exception.Code);
    }

    [Fact]
    public void HandlerExceptionsStayInsideMessageBoundary()
    {
        using var registration = _channel.Register<object?, bool>("managed.fail", _ => throw new InvalidOperationException("Failure Ω"));
        var exception = Assert.Throws<MessageException>(() => _channel.Call<bool>("managed.fail", null));
        Assert.Equal("handler_failed", exception.Code);
        Assert.Equal("Failure Ω", exception.Message);
    }

    [Fact]
    public void DuplicateRegistrationDoesNotReplaceOriginal()
    {
        using var registration = _channel.Register<object?, int>("managed.unique", _ => 1);
        Assert.Throws<InvalidOperationException>(() => _channel.Register<object?, int>("managed.unique", _ => 2));
        Assert.Equal(1, _channel.Call<int>("managed.unique", null));
    }

    [Fact]
    public void LargeRequestIsRejectedBeforeNativeCall()
    {
        Assert.Throws<ArgumentException>(() => _channel.Call<string>("native.echo", new string('x', 1024 * 1024)));
    }

    [Fact]
    public async Task RemovingHandlerWaitsForActiveCall()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var registration = _channel.Register<object?, int>("managed.wait", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException();
            }
            return 7;
        });
        var invocation = Task.Run(() => _channel.Call<int>("managed.wait", null));
        Task? removal = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            removal = Task.Run(registration.Dispose);
            Assert.NotSame(removal, await Task.WhenAny(removal, Task.Delay(100)));
        }
        finally
        {
            release.Set();
            await invocation.WaitAsync(TimeSpan.FromSeconds(10));
            if (removal is not null)
            {
                await removal.WaitAsync(TimeSpan.FromSeconds(10));
            }
            registration.Dispose();
        }
        Assert.Equal(7, await invocation);
        Assert.Throws<MessageException>(() => _channel.Call<int>("managed.wait", null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModLoaderLoadsOrRollsBackPartialStartup(bool failStartup)
    {
        var root = Path.Combine(Path.GetTempPath(), "F4ForgeTests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "Fixture");
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixture.dll"), Path.Combine(directory, "Fixture.dll"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "FixtureDependency.dll"), Path.Combine(directory, "FixtureDependency.dll"));
        if (failStartup)
        {
            File.WriteAllText(Path.Combine(directory, "fail-start"), "");
        }
        var errors = new List<string>();
        using var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Add);
        try
        {
            loader.LoadDirectory(root);
            Assert.Equal(failStartup ? 0 : 1, loader.Count);
            Assert.Equal(failStartup ? 1 : 0, errors.Count);
            if (!failStartup)
            {
                Assert.Equal("Loaded", _channel.Call<string>("fixture.echo", "Loaded"));
            }
            var received = new List<string>();
            using var listener = _channel.Subscribe<string>("fixture.output", received.Add);
            _channel.Publish("fixture.input", "Before stop");
            Assert.Equal(failStartup ? 0 : 1, received.Count);
            loader.Dispose();
            _channel.Publish("fixture.input", "After stop");
            Assert.Equal(failStartup ? 0 : 1, received.Count);
            Assert.Throws<MessageException>(() => _channel.Call<string>("fixture.echo", null));
        }
        finally
        {
            loader.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InteropLayoutsMatchNativeX64()
    {
        Assert.Equal(64, Marshal.SizeOf<ChannelBinding>());
        Assert.Equal(72, Marshal.SizeOf<Bootstrap.Arguments>());
    }

    public void Dispose()
    {
        Destroy();
    }
}
