using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Actors.Commands;
using F4Forge.Net.Actors.Queries;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Hosting;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Operations;
using F4Forge.Net.Player.Queries;
using F4Forge.Net.Serialization;

namespace F4Forge.Net.Tests;

public sealed partial class FoundationTests : IDisposable
{
    private readonly List<IDisposable> _registrations = [];

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticsKeepDllOwnershipNestedCallsAndFullErrors(bool binary)
    {
        var channel = Open(binary);
        var records = Capture(channel);
        var first = Context(channel, "First.dll");
        var second = Context(channel, "Second.dll");
        Track(first.Messages.Register<int, int>("first", value => second.Messages.Call<int>("second", value)));
        Track(second.Messages.Register<int, int>("second", value => value + 1));

        Assert.Equal(8, first.Messages.Call<int>("first", 7));
        var outer = Assert.Single(records, entry => entry.Name == "first" && entry.Kind == "message.call");
        var handler = Assert.Single(records, entry => entry.Name == "first" && entry.Kind == "message.handle");
        var nested = Assert.Single(records, entry => entry.Name == "second" && entry.Kind == "message.call");
        var nestedHandler = Assert.Single(records, entry => entry.Name == "second" && entry.Kind == "message.handle");
        Assert.Equal(first.Diagnostics.ModId, handler.ModId);
        Assert.Equal(second.Diagnostics.ModId, nestedHandler.ModId);
        Assert.Equal(outer.Id, handler.ParentId);
        Assert.Equal(handler.Id, nested.ParentId);
        Assert.Equal(nested.Id, nestedHandler.ParentId);
        Assert.All(records, entry =>
        {
            Assert.True(entry.Success);
            Assert.True(entry.ThreadId > 0);
            Assert.True(entry.ElapsedMilliseconds >= 0);
        });
        Assert.Null(DiagnosticTrace.CurrentOwner);

        Track(first.Messages.Register<int, int>("broken", _ => throw new InvalidOperationException("Ошибка 🌍")));
        Assert.Throws<MessageException>(() => second.Messages.Call<int>("broken", 0));
        var error = Assert.Single(records, entry => entry.Name == "broken" && entry.Kind == "message.handle");
        Assert.False(error.Success);
        Assert.Equal(first.Diagnostics.ModId, error.ModId);
        Assert.Equal(typeof(InvalidOperationException).FullName, error.ExceptionType);
        Assert.Contains("Ошибка 🌍", error.Message);
        Assert.Contains(nameof(DiagnosticsKeepDllOwnershipNestedCallsAndFullErrors), error.StackTrace);
    }

    [Fact]
    public void DiagnosticsCoverDeserializationAndIsolatedEvents()
    {
        var channel = Open();
        var records = Capture(channel);
        var context = Context(channel);
        Track(context.Messages.Register<int, int>("typed", value => value));
        Assert.Throws<MessageException>(() => channel.Call<int>("typed", "not an integer"));
        Assert.False(Assert.Single(records).Success);

        var delivered = 0;
        Track(context.Events.Subscribe<int>("example", (Action<int>)(_ => throw new InvalidOperationException("Event failed"))));
        Track(context.Events.Subscribe<int>("example", _ => delivered++));
        context.Events.Publish("example", 1);
        Assert.Equal(1, delivered);
        Assert.Contains(records, entry => entry.Kind == "event.handle" && !entry.Success && entry.Message == "Event failed");
        Assert.Contains(records, entry => entry.Kind == "event.handle" && entry.Success);
        Assert.Null(DiagnosticTrace.CurrentOwner);
    }

    [Fact]
    public void DiagnosticSinkFailurePreservesResultsAndOriginalExceptions()
    {
        var channel = Open();
        Track(channel.Register<DiagnosticRecord, bool>("diagnostics.record", _ => throw new InvalidOperationException("Sink failed")));
        var context = Context(channel);
        Assert.Equal(9, context.Messages.Call<int>("native.echo", 9));
        Track(context.Messages.Register<int, int>("broken", _ => throw new InvalidOperationException("Original failure")));
        var error = Assert.Throws<MessageException>(() => context.Messages.Call<int>("broken", 0));
        Assert.Equal("Original failure", error.Message);
        Assert.Null(DiagnosticTrace.CurrentOwner);
    }

    [Fact]
    public void DiagnosticsQueryOwnHistoryAndHookStatusWithoutTracingThemselves()
    {
        var channel = Open();
        var records = Capture(channel);
        var context = Context(channel);
        Track(channel.Register<JsonElement, DiagnosticRecord[]>("diagnostics.operations", arguments =>
        {
            Assert.Equal(context.Diagnostics.ModId, arguments.GetProperty("modId").GetString());
            Assert.Equal(2, arguments.GetProperty("maximum").GetInt32());
            return records.ToArray();
        }));
        Track(channel.Register<object?, HookStatus[]>("diagnostics.hooks", _ => [new("console", "function", "failed", "Invalid target", 1)]));
        context.Messages.Call<int>("native.echo", 1);
        Assert.Single(context.Diagnostics.GetOperations(2));
        Assert.Equal("failed", Assert.Single(context.Diagnostics.GetHooks()).State);
        Assert.Single(records);
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Diagnostics.GetOperations(0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedObjectsUseSharedSerializerAndAllowExternalViews(bool binary)
    {
        var channel = Open(binary);
        var context = Context(channel);
        Track(channel.Register<JsonElement, FormSnapshot?>("forms.describe", arguments => arguments.GetProperty("formId").GetUInt32() switch
        {
            0 => null,
            1 => Snapshot(1, "WEAP"),
            2 => Snapshot(2, "ACHR", actor: true),
            _ => Snapshot(3, "AVIF", value: true)
        }));
        Track(channel.Register<object?, FormSnapshot>("forms.player", _ => Snapshot(2, "ACHR", actor: true)));
        Track(channel.Register<JsonElement, FormSnapshot>("forms.resolve", arguments =>
        {
            Assert.True(arguments.TryGetProperty("plugin", out _) || arguments.TryGetProperty("editorId", out _));
            return Snapshot(1, "WEAP");
        }));
        Track(channel.Register<JsonElement, ReferenceSnapshot>("forms.reference", arguments =>
        {
            Assert.Equal(7UL, arguments.GetProperty("session").GetUInt64());
            return new ReferenceSnapshot(10, 20, 0, new Position(1, 2, 3));
        }));
        Assert.Null(context.Objects.Query(new FormById<Form>(0)));
        Assert.IsType<Weapon>(context.Objects.Query(new FormById<Weapon>(1)));
        Assert.IsType<Weapon>(context.Objects.Query(new FormByPlugin<Weapon>("Example.esp", 1)));
        Assert.IsType<Weapon>(context.Objects.Query(new FormByEditorId<Weapon>("ExampleWeapon")));
        Assert.Throws<InvalidCastException>(() => context.Objects.Query(new FormById<Actor>(1)));
        Assert.True(context.Objects.Query(new FormById<Form>(2))!.Is<Reference>());
        var actor = context.Objects.Query(new GetPlayer())!;
        Assert.Equal(new Position(1, 2, 3), context.Messages.Query(new GetReferenceSnapshot(actor)).Position);
        Assert.Equal(0U, context.Messages.Query(new GetReferenceSnapshot(actor)).LocationFormId);

        Track(channel.Register<JsonElement, float>("forms.actor_value", arguments =>
        {
            Assert.Equal(2U, arguments.GetProperty("formId").GetUInt32());
            Assert.Equal(3U, arguments.GetProperty("actorValueFormId").GetUInt32());
            Assert.Equal("permanent", arguments.GetProperty("kind").GetString());
            return 75.5f;
        }));
        Track(channel.Register<JsonElement, bool>("forms.set_base_value", arguments =>
        {
            Assert.Equal(90f, arguments.GetProperty("value").GetSingle());
            return true;
        }));
        var value = context.Objects.Query(new FormById<ActorValue>(3))!;
        Assert.Equal(75.5f, context.Messages.Query(new GetActorValue(actor, value, ActorValueKind.Permanent)));
        context.Messages.Execute(new SetActorBaseValue(actor, value, 90));
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Messages.Execute(new SetActorBaseValue(actor, value, float.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => context.Messages.Query(new GetActorValue(actor, value, (ActorValueKind)999)));
        var foreignValue = Context(channel, "Other.dll").Objects.Query(new FormById<ActorValue>(3))!;
        Assert.Throws<ArgumentException>(() => context.Messages.Query(new GetActorValue(actor, foreignValue)));
        var oldValue = new Form(context.Messages, Snapshot(3, "AVIF", value: true) with { Session = 6 }).As<ActorValue>();
        Assert.Throws<ArgumentException>(() => context.Messages.Query(new GetActorValue(actor, oldValue)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormQueriesValidateResultsAndAllowModuleOwnedLookups(bool binary)
    {
        var channel = Open(binary);
        var context = Context(channel);
        var playerCalls = 0;
        Track(channel.Register<object?, FormSnapshot?>("forms.player", arguments =>
        {
            Assert.Null(arguments);
            playerCalls++;
            return playerCalls switch
            {
                1 => null,
                2 => Snapshot(1, "WEAP"),
                _ => Snapshot(2, "ACHR", actor: true)
            };
        }));
        Assert.Null(context.Objects.Query(new GetPlayer()));
        Assert.Throws<InvalidCastException>(() => context.Objects.Query(new GetPlayer()));
        Actor? player = context.Objects.Query(new GetPlayer());
        Assert.NotNull(player);
        Assert.Same(context.Messages, player.Messages);
        Assert.Equal(7UL, player.Session);

        Track(channel.Register<JsonElement, FormSnapshot>("example.weapon", arguments =>
        {
            var property = Assert.Single(arguments.EnumerateObject());
            Assert.Equal("editorId", property.Name);
            Assert.Equal("Оружие Ω", property.Value.GetString());
            return Snapshot(1, "WEAP");
        }));
        Weapon? weapon = context.Objects.Query(new WeaponQuery("Оружие Ω"));
        Assert.NotNull(weapon);
        Assert.Same(context.Messages, weapon.Messages);

        Track(channel.Register<JsonElement, FormSnapshot>("forms.resolve", arguments =>
        {
            Assert.Equal("Мод Ω.esl", arguments.GetProperty("plugin").GetString());
            Assert.Equal(0x123U, arguments.GetProperty("localId").GetUInt32());
            Assert.Equal(2, arguments.EnumerateObject().Count());
            return Snapshot(1, "WEAP");
        }));
        Assert.NotNull(context.Objects.Query(new FormByPlugin<Weapon>("Мод Ω.esl", 0x123)));
        Assert.Throws<ArgumentException>(() => context.Objects.Query(new FormByPlugin<Form>(" ", 1)));
        Assert.Throws<ArgumentException>(() => context.Objects.Query(new FormByEditorId<Form>(" ")));
        Assert.Throws<ArgumentNullException>(() => context.Objects.Query<Actor>(null!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedOperationsPreserveWireDataResultsAndErrorBoundaries(bool binary)
    {
        var channel = Open(binary);
        var context = Context(channel);
        var commandCalls = 0;
        Track(channel.Register<JsonElement, string[]>("example.names", arguments =>
        {
            var property = Assert.Single(arguments.EnumerateObject());
            Assert.Equal("prefix", property.Name);
            Assert.Equal("Игрок Ω", property.Value.GetString());
            return ["Первый", "Второй 🌍"];
        }));
        Track(channel.Register<JsonElement, bool>("example.mode", arguments =>
        {
            commandCalls++;
            var property = Assert.Single(arguments.EnumerateObject());
            Assert.Equal("enabled", property.Name);
            return property.Value.GetBoolean();
        }));
        string[] names = context.Messages.Query(new GetNames("Игрок Ω"));
        Assert.Equal(new[] { "Первый", "Второй 🌍" }, names);
        Assert.False(context.Messages.Execute(new SetMode(false)));
        Assert.True(context.Messages.Execute(new SetMode(true)));
        Assert.Equal(2, commandCalls);

        Track(channel.Register<object?, string?>("example.optional", arguments =>
        {
            Assert.Null(arguments);
            return null;
        }));
        Assert.Null(context.Messages.Query(new GetOptionalName()));
        Track(channel.Register<JsonElement, bool>("example.failure", _ =>
            throw new InvalidOperationException("Ошибка изменения Ω")));
        var error = Assert.Throws<MessageException>(() => context.Messages.Execute(new FailingCommand()));
        Assert.Equal("Ошибка изменения Ω", error.Message);
        Assert.Throws<ArgumentException>(() => context.Messages.Query(new GetNames(" ")));
        Assert.Throws<ArgumentNullException>(() => context.Messages.Query<string[]>(null!));
        Assert.Throws<ArgumentNullException>(() => context.Messages.Execute<bool>(null!));
        Assert.Throws<ArgumentNullException>(() => OperationExtensions.Query(null!, new GetOptionalName()));
        Assert.Throws<ArgumentNullException>(() => OperationExtensions.Execute(null!, new SetMode(true)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormOperationsRejectForeignScopesAndForwardSessionIdentity(bool binary)
    {
        var channel = Open(binary);
        var context = Context(channel);
        var foreignContext = Context(channel, "Foreign.dll");
        var actor = new Form(context.Messages, Snapshot(2, "ACHR", actor: true)).As<Actor>();
        var value = new Form(context.Messages, Snapshot(3, "AVIF", value: true)).As<ActorValue>();
        var foreignActor = new Form(foreignContext.Messages, actor.Snapshot).As<Actor>();
        var nativeCalls = 0;
        Track(channel.Register<JsonElement, FormSnapshot?>("forms.describe", arguments =>
        {
            nativeCalls++;
            Assert.Equal(2U, arguments.GetProperty("formId").GetUInt32());
            Assert.Equal(7UL, arguments.GetProperty("session").GetUInt64());
            Assert.Equal(2, arguments.EnumerateObject().Count());
            return null;
        }));
        Assert.Throws<ArgumentException>(() => foreignContext.Messages.Query(new DescribeForm(actor)));
        Assert.Throws<ArgumentException>(() => foreignContext.Messages.Query(new GetReferenceSnapshot(actor)));
        Assert.Throws<ArgumentException>(() => foreignContext.Messages.Query(new GetActorValue(actor, value)));
        Assert.Throws<ArgumentException>(() => foreignContext.Messages.Execute(new SetActorBaseValue(actor, value, 1)));
        Assert.Throws<ArgumentException>(() => context.Messages.Query(
            new GetReferenceSnapshot(actor) with { Reference = foreignActor }));
        Assert.Throws<ArgumentException>(() => context.Messages.Query(
            new GetActorValue(actor, value) with { Actor = foreignActor }));
        Assert.Throws<ArgumentNullException>(() => context.Messages.Query(new DescribeForm(null!)));
        Assert.Throws<ArgumentNullException>(() => context.Messages.Execute(new SetActorBaseValue(actor, null!, 1)));
        Assert.Equal(0, nativeCalls);
        Assert.Null(context.Messages.Query(new DescribeForm(actor)));
        Assert.Equal(1, nativeCalls);
    }

    [Fact]
    public void ClosingModScopeRevokesRetainedObjectsAndCallbacks()
    {
        var channel = Open();
        Track(channel.Register<object?, FormSnapshot>("forms.player", _ => Snapshot(2, "ACHR", actor: true)));
        var lifetime = new ModLifetime();
        using var messages = new ScopedChannel(channel, lifetime);
        using var events = new ScopedEvents(channel, lifetime);
        var context = new ModContext(Path.GetTempPath(), messages, events, channel.Serializer);
        var actor = context.Objects.Query(new GetPlayer())!;
        context.Messages.Register<int, int>("owned", value => value);
        context.Events.Subscribe<int>("owned.event", _ => throw new InvalidOperationException());
        events.Dispose();
        messages.Dispose();
        Assert.Throws<ObjectDisposedException>(() => context.Messages.Query(new DescribeForm(actor)));
        var value = new Form(context.Messages, Snapshot(3, "AVIF", value: true)).As<ActorValue>();
        Assert.Throws<ObjectDisposedException>(() => context.Messages.Execute(new SetActorBaseValue(actor, value, 1)));
        Assert.Throws<MessageException>(() => channel.Call<int>("owned", 1));
        channel.Publish("owned.event", 1);
    }

    private NativeChannel Open(bool binary = false)
    {
        return binary ? new NativeChannel(CreateBinary(), new TaggedSerializer()) : new NativeChannel(Create());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LifecycleDiagnosticsCaptureStartupRollbackAndEveryCleanupStage(bool fail)
    {
        var channel = Open();
        var records = Capture(channel);
        var directory = Path.Combine(Path.GetTempPath(), "F4Forge.Foundation.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var name in new[] { "Fixture.dll", "FixtureDependency.dll" })
            {
                File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(directory, name));
            }
            if (fail)
            {
                File.WriteAllText(Path.Combine(directory, "fail-start"), string.Empty);
            }
            var errors = new List<string>();
            var cleanupLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
            await using (var loader = new ModLoader(channel, channel, channel.Serializer, errors.Add, cleanupLog.Enqueue))
            {
                loader.LoadDirectory(directory);
                Assert.Equal(fail ? 0 : 1, loader.Count);
            }
            var modId = Path.Combine(directory, "Fixture.dll");
            var ownRecords = records.Where(entry => entry.ModId == modId).ToArray();
            Assert.Equal(!fail, Assert.Single(ownRecords, entry => entry.Kind == "mod.start").Success);
            Assert.Equal(!fail, Assert.Single(ownRecords, entry => entry.Kind == "mod.load").Success);
            Assert.Equal(["events", "messages", "instance", "assembly"],
                cleanupLog.Where(line => line.Contains(modId, StringComparison.Ordinal))
                    .Select(line => line.Split("mod.stop/", StringSplitOptions.None)[1].Split(':')[0]).ToArray());
            Assert.Throws<MessageException>(() => channel.Call<string>("fixture.directory", null));
            Assert.Equal(fail ? 1 : 0, errors.Count);
            Assert.Null(DiagnosticTrace.CurrentOwner);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private List<DiagnosticRecord> Capture(NativeChannel channel)
    {
        var records = new List<DiagnosticRecord>();
        Track(channel.Register<DiagnosticRecord, bool>("diagnostics.record", entry =>
        {
            records.Add(entry);
            return true;
        }));
        return records;
    }

    private static ModContext Context(NativeChannel channel, string filename = "Example.dll")
    {
        var directory = Path.Combine(Path.GetTempPath(), "F4Forge.Foundation.Tests");
        return new ModContext(directory, channel, channel, channel.Serializer, Path.Combine(directory, filename));
    }

    private static FormSnapshot Snapshot(uint id, string type, bool actor = false, bool value = false)
    {
        return new FormSnapshot(id, 7, type, "Имя 🌍", "Example", actor, actor, value);
    }

    private void Track(IDisposable registration)
    {
        _registrations.Add(registration);
    }

    public void Dispose()
    {
        foreach (var registration in _registrations.AsEnumerable().Reverse())
        {
            registration.Dispose();
        }
        Destroy();
    }

    private sealed record GetNames(string Prefix) : IQuery<string[]>
    {
        public string MethodName => "example.names";
        public object Arguments => new { Prefix };

        public void Validate(IMessageChannel messages)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Prefix);
        }
    }

    private sealed record GetOptionalName : IQuery<string?>
    {
        public string MethodName => "example.optional";
        public object? Arguments => null;
    }

    private sealed record SetMode(bool Enabled) : ICommand<bool>
    {
        public string MethodName => "example.mode";
        public object Arguments => new { Enabled };
    }

    private sealed record FailingCommand : ICommand<bool>
    {
        public string MethodName => "example.failure";
        public object Arguments => new { };
    }

    private sealed record WeaponQuery(string EditorId) : IFormQuery<Weapon>
    {
        public string MethodName => "example.weapon";
        public object Arguments => new { EditorId };
    }

    private sealed class Weapon : Form, IFormView<Weapon>
    {
        private Weapon(Form source) : base(source) { }
        public new static bool Matches(FormSnapshot snapshot) => snapshot.Type == "WEAP";
        public new static Weapon Create(Form source) => new(source);
    }

    private sealed class TaggedSerializer : IDataSerializer
    {
        private readonly JsonDataSerializer _json = new();
        public string Format => "test.binary";

        public byte[] Serialize<T>(T value) => [0, 255, .. _json.Serialize(value)];

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            if (data.Length < 2 || data[0] != 0 || data[1] != 255)
            {
                throw new InvalidDataException("Missing binary prefix.");
            }
            return _json.Deserialize<T>(data[2..]);
        }
    }
}
