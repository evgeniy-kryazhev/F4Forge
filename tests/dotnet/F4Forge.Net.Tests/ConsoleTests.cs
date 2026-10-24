using System.Runtime.InteropServices;
using F4Forge.Net.Console;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Serialization;
using static F4Forge.Net.Singletons.SingletonExtensions;

namespace F4Forge.Net.Tests;

public sealed partial class ConsoleTests : IDisposable
{
    private readonly NativeChannel _channel;
    private IDisposable? _validation;

    public ConsoleTests()
    {
        _channel = new NativeChannel(Create());
        _validation = _channel.Register<AliasRequest, bool>("console.validate_alias", request => request.Alias != "help");
    }

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Fact]
    public void ModContextExposesTheGameConsole()
    {
        var context = new ModContext(@"C:\mods\Hello", _channel, _channel, _channel.Serializer);
        using var registration = context.GetSingleton<GameConsole>().AddHandler("hello", command => command.WriteLine("connected"));
        Assert.Equal(["connected"], Invoke("hello").Lines);
    }

    [Fact]
    public void HandlerReceivesInvocationAndReturnsLiteralOutput()
    {
        var console = new GameConsole(_channel);
        using var registration = console.AddHandler("Hello", command =>
        {
            Assert.Equal("hello", command.Alias);
            Assert.Equal(["World Ω", ""], command.Arguments);
            Assert.Equal("hello \"World Ω\" \"\"", command.RawCommand);
            Assert.Equal("\"World Ω\" \"\"", command.RawArguments);
            Assert.Equal(0x14u, command.SelectedReferenceFormId);
            command.WriteLine("Hello Ω {name} %s!");
        });

        var result = Invoke("hello");

        Assert.Equal(["Hello Ω {name} %s!"], result.Lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1hello")]
    [InlineData("hello world")]
    [InlineData("helloΩ")]
    [InlineData("hello;quit")]
    public void InvalidAliasesAreRejected(string alias)
    {
        var console = new GameConsole(_channel);
        Assert.Throws<ArgumentException>(() => console.AddHandler(alias, _ => { }));
    }

    [Fact]
    public void NullAndReservedAliasesAreRejected()
    {
        var console = new GameConsole(_channel);
        Assert.Throws<ArgumentNullException>(() => console.AddHandler(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => console.AddHandler("hello", null!));
        Assert.Throws<InvalidOperationException>(() => console.AddHandler("HELP", _ => { }));
    }

    [Fact]
    public void CaseInsensitiveDuplicatesPreserveOriginalAndDisposalAllowsReuse()
    {
        var console = new GameConsole(_channel);
        var first = console.AddHandler("hello", command => command.WriteLine("first"));
        Assert.Throws<InvalidOperationException>(() => console.AddHandler("HELLO", _ => { }));
        Assert.Equal(["first"], Invoke("hello").Lines);
        first.Dispose();
        first.Dispose();
        Assert.Equal("method_not_found", Assert.Throws<MessageException>(() => Invoke("hello")).Code);
        using var second = console.AddHandler("HELLO", command => command.WriteLine("second"));
        Assert.Equal(["second"], Invoke("hello").Lines);
    }

    [Fact]
    public void ExceptionsAndParseErrorsDoNotEscapeTheChannel()
    {
        var console = new GameConsole(_channel);
        var calls = 0;
        using var registration = console.AddHandler("hello", _ =>
        {
            calls++;
            throw new InvalidOperationException("Handler failed");
        });
        var parseFailure = Assert.Throws<MessageException>(() => Invoke("hello", "Unclosed quote"));
        Assert.Equal("handler_failed", parseFailure.Code);
        Assert.Equal("Unclosed quote", parseFailure.Message);
        Assert.Equal(0, calls);
        Assert.Equal("Handler failed", Assert.Throws<MessageException>(() => Invoke("hello")).Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ContextCannotWriteAfterSuccessOrFailure()
    {
        var console = new GameConsole(_channel);
        ConsoleCommandContext? captured = null;
        using var success = console.AddHandler("hello", command =>
        {
            captured = command;
            Assert.Throws<ArgumentNullException>(() => command.WriteLine(null!));
        });
        Assert.Empty(Invoke("hello").Lines);
        Assert.Throws<InvalidOperationException>(() => captured!.WriteLine("late"));
        using var failure = console.AddHandler("broken", command =>
        {
            captured = command;
            throw new InvalidOperationException("Failed");
        });
        Assert.Throws<MessageException>(() => Invoke("broken"));
        Assert.Throws<InvalidOperationException>(() => captured!.WriteLine("late"));
    }

    [Fact]
    public void ModShutdownRemovesCommandsAndAllowsAnotherModToReuseAlias()
    {
        using var scope = new ScopedChannel(_channel, new ModLifetime());
        var console = new GameConsole(scope);
        console.AddHandler("hello", command => command.WriteLine("running"));
        Assert.Equal(["running"], Invoke("hello").Lines);
        scope.Dispose();
        Assert.Equal("method_not_found", Assert.Throws<MessageException>(() => Invoke("hello")).Code);
        Assert.Throws<ObjectDisposedException>(() => console.AddHandler("hello", _ => { }));
        using var replacement = new GameConsole(_channel).AddHandler("hello", _ => { });
        Assert.Empty(Invoke("hello").Lines);
    }

    [Fact]
    public void CommandsUseTheConfiguredBinarySerializer()
    {
        _validation!.Dispose();
        _validation = null;
        Destroy();
        var serializer = new TaggedSerializer();
        var channel = new NativeChannel(CreateBinary(), serializer);
        using var validation = channel.Register<AliasRequest, bool>("console.validate_alias", _ => true);
        using var registration = new GameConsole(channel).AddHandler("hello", command => command.WriteLine("World Ω"));
        var result = channel.Call<ConsoleResult>("console.command.hello", Invocation());
        Assert.Equal(["World Ω"], result.Lines);
        Assert.True(serializer.Reads >= 4);
        Assert.True(serializer.Writes >= 4);
    }

    private ConsoleResult Invoke(string alias, string parseError = "")
    {
        return _channel.Call<ConsoleResult>("console.command." + alias, Invocation() with { Alias = alias, ParseError = parseError });
    }

    private static ConsoleInvocation Invocation()
    {
        return new ConsoleInvocation("hello", ["World Ω", ""], "hello \"World Ω\" \"\"", "\"World Ω\" \"\"", 0x14, "");
    }

    public void Dispose()
    {
        _validation?.Dispose();
        Destroy();
    }

    private sealed record AliasRequest(string Alias);

    private sealed class TaggedSerializer : IDataSerializer
    {
        private readonly JsonDataSerializer _json = new();
        public string Format => "test.binary";
        public int Reads { get; private set; }
        public int Writes { get; private set; }

        public byte[] Serialize<T>(T value)
        {
            Writes++;
            return [0, 255, .. _json.Serialize(value)];
        }

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            Reads++;
            Assert.True(data.Length >= 2 && data[0] == 0 && data[1] == 255);
            return _json.Deserialize<T>(data[2..]);
        }
    }
}
