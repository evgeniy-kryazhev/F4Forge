using System.Runtime.InteropServices;
using F4Forge.Net.Interop;
using F4Forge.Net.Logging;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Tests;

public sealed partial class LoggingTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("error")]
    public void ExtensionsUseSharedChannelAndPreservePayload(string level)
    {
        LogRequest? received = null;
        using var registration = _channel.Register<LogRequest, bool>("logging.write", request =>
        {
            received = request;
            return true;
        });
        var context = new ModContext(@"C:\mods\HelloMod\", _channel, _channel, _channel.Serializer);
        const string message = "Hello 🌍 {value}";
        switch (level)
        {
            case "info":
                context.LogInfo(message);
                break;
            case "warning":
                context.LogWarning(message);
                break;
            case "error":
                context.LogError(message);
                break;
        }
        Assert.Equal(new LogRequest(level, "HelloMod", message), received);
    }

    [Fact]
    public void ExtensionsRejectNullMessage()
    {
        var context = new ModContext(@"C:\mods\HelloMod", _channel, _channel, _channel.Serializer);
        Assert.Throws<ArgumentNullException>(() => context.LogInfo(null!));
    }

    public void Dispose()
    {
        Destroy();
    }

    public sealed record LogRequest(string Level, string Source, string Message);
}
