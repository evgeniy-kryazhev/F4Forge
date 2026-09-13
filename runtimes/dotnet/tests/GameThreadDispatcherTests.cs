using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Scheduling;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed class GameThreadDispatcherTests
{
    private static ulong queuedTaskId;

    [Fact]
    public async Task QueuedActionCompletesWhenNativeTaskExecutes()
    {
        using var harness = new SchedulerHarness();
        var scheduler = harness.Scheduler;
        var dispatcher = new GameThreadDispatcher(scheduler);
        var calls = 0;

        var task = dispatcher.InvokeAsync(() => ++calls);
        Assert.False(task.IsCompleted);
        scheduler.Execute(queuedTaskId);

        await task;
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GenericInvocationReturnsResultAndPropagatesException()
    {
        using var harness = new SchedulerHarness();
        var scheduler = harness.Scheduler;
        var dispatcher = new GameThreadDispatcher(scheduler);

        var result = dispatcher.InvokeAsync(() => 42);
        scheduler.Execute(queuedTaskId);
        Assert.Equal(42, await result);

        var failed = dispatcher.InvokeAsync<int>(() => throw new InvalidOperationException("failure"));
        scheduler.Execute(queuedTaskId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failed);
    }

    [Fact]
    public async Task CancellationAndShutdownCompletePendingTasks()
    {
        using var harness = new SchedulerHarness();
        var scheduler = harness.Scheduler;
        var dispatcher = new GameThreadDispatcher(scheduler);
        using var cancellation = new CancellationTokenSource();
        var cancelled = dispatcher.InvokeAsync(() => { }, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => cancelled);

        var shutdown = dispatcher.InvokeAsync(() => { });
        scheduler.Dispose();
        await Assert.ThrowsAsync<TaskCanceledException>(() => shutdown);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.InvokeAsync(() => { }));
    }

    private sealed unsafe class SchedulerHarness : IDisposable
    {
        private readonly NativeApi* _host;

        internal SchedulerHarness()
        {
            _host = (NativeApi*)NativeMemory.Alloc((nuint)sizeof(NativeApi));
            *_host = new NativeApi { QueueTask = &QueueTask };
            Scheduler = new ManagedTaskScheduler(_host, _host, 7);
        }

        internal ManagedTaskScheduler Scheduler { get; }

        public void Dispose()
        {
            Scheduler.Dispose();
            NativeMemory.Free(_host);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int QueueTask(void* context, ulong runtime, ulong taskId)
    {
        queuedTaskId = taskId;
        return (int)F4ForgeResult.Success;
    }
}
