using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Actors.Commands;
using F4Forge.Net.Events;
using F4Forge.Net.Interop;
using F4Forge.Net.Interception;
using F4Forge.Net.Mods;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Operations;
using F4Forge.Net.Player.Queries;
using F4Forge.Net.Serialization;

namespace F4Forge.Net.Tests;

public sealed partial class AsyncSerializerTests
{
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationsInterceptorsQueriesAndCommandsAfterAwaitUseSharedSerializer(bool binary)
    {
        var channel = binary ? new NativeChannel(CreateBinary(), new TaggedSerializer()) : new NativeChannel(Create());
        var lifetime = new ModLifetime();
        using var messages = new ScopedChannel(channel, lifetime);
        using var events = new ScopedEvents(channel, lifetime);
        var context = new ModContext(Path.GetTempPath(), messages, events, channel.Serializer, "SerializerMod.dll", lifetime);
        var queued = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<Position?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gameThread = 0;
        using var threadCheck = channel.Register<object?, bool>("threading.is_game_thread", _ =>
            Environment.CurrentManagedThreadId == Volatile.Read(ref gameThread));
        using var enqueue = channel.Register<JsonElement, bool>("threading.enqueue", data =>
        {
            queued.TrySetResult(data.GetProperty("taskId").GetString()!);
            return true;
        });
        using var player = channel.Register<object?, FormSnapshot>("forms.player", _ =>
        {
            Assert.Equal(Volatile.Read(ref gameThread), Environment.CurrentManagedThreadId);
            return new FormSnapshot(0x14, 1, "ACHR", "Игрок Ω", "Player", true, true, false);
        });
        using var snapshot = channel.Register<JsonElement, ReferenceSnapshot>("forms.reference", _ =>
            new ReferenceSnapshot(7, 9, 11, new Position(10, 20, 30)));
        var writes = 0;
        using var setBaseValue = channel.Register<JsonElement, bool>("forms.set_base_value", arguments =>
        {
            Assert.Equal(Volatile.Read(ref gameThread), Environment.CurrentManagedThreadId);
            Assert.Equal(0x14U, arguments.GetProperty("formId").GetUInt32());
            Assert.Equal(1UL, arguments.GetProperty("session").GetUInt64());
            Assert.Equal(0x2D4U, arguments.GetProperty("actorValueFormId").GetUInt32());
            Assert.Equal(1UL, arguments.GetProperty("actorValueSession").GetUInt64());
            Assert.Equal(90f, arguments.GetProperty("value").GetSingle());
            writes++;
            return true;
        });
        try
        {
            using var interceptor = context.Interceptors.OnConsoleCommandExecuting(input => input.Command = "help Игрок Ω");
            var decision = channel.Call<JsonElement>("test.intercept", new
            {
                OriginalCommand = "before", Command = "before", Skip = false, SelectedReferenceFormId = 0x14
            });
            Assert.Equal("help Игрок Ω", decision.GetProperty("command").GetString());
            using var subscription = context.Events.Subscribe<AfterAwait>(async (_, token) =>
            {
                await scheduled.Task;
                var position = await context.GameThread.RunAsync(
                    () =>
                    {
                        var player = context.Objects.Query(new GetPlayer());
                        if (player is null)
                        {
                            return (Position?)null;
                        }

                        var value = new Form(context.Messages,
                            new FormSnapshot(0x2D4, 1, "AVIF", "Здоровье", "Health", false, false, true)).As<ActorValue>();
                        Assert.True(context.Messages.Execute(new SetActorBaseValue(player, value, 90)));
                        return context.Messages.Query(new GetReferenceSnapshot(player)).Position;
                    }, token);
                completed.SetResult(position);
            });
            channel.Publish(new AfterAwait());
            scheduled.SetResult();
            var id = await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // A notification on an arbitrary thread must not execute a game task.
            channel.Publish("threading.execute", new { TaskId = id });
            Assert.False(completed.Task.IsCompleted);
            Volatile.Write(ref gameThread, Environment.CurrentManagedThreadId);
            channel.Publish("threading.execute", new { TaskId = id });
            Assert.Equal(new Position(10, 20, 30), await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(1, writes);
            lifetime.Close();
            await lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            lifetime.Close();
            events.Dispose();
            messages.Dispose();
            threadCheck.Dispose();
            enqueue.Dispose();
            player.Dispose();
            snapshot.Dispose();
            setBaseValue.Dispose();
            channel.DetachTransport();
            Destroy();
        }
    }

    private sealed record AfterAwait : IGameEvent
    {
        public static string EventName => "after.await";
    }

    private sealed class TaggedSerializer : IDataSerializer
    {
        private readonly JsonDataSerializer _json = new();
        public string Format => "test.binary";

        public byte[] Serialize<T>(T value)
        {
            return [0, 255, .. _json.Serialize(value)];
        }

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
