using System.Collections.Concurrent;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Scheduling;

internal unsafe sealed class ManagedTaskScheduler : IGameThreadScheduler, IDisposable
{
    private readonly ConcurrentDictionary<ulong, WorkItem> _tasks = new();
    private readonly NativeApi* _host;
    private readonly void* _context;
    private readonly ulong _runtime;
    private long _nextTaskId;
    private int _quiescing;

    internal ManagedTaskScheduler(NativeApi* host, void* context, ulong runtime)
    {
        _host = host;
        _context = context;
        _runtime = runtime;
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken) =>
        Queue<object?>(() => { action(); return null; }, cancellationToken);

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken) =>
        Queue(action, cancellationToken);

    internal void Execute(ulong taskId)
    {
        if (_tasks.TryRemove(taskId, out var task)) task.Execute();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _quiescing, 1) != 0) return;
        foreach (var pair in _tasks)
            if (_tasks.TryRemove(pair.Key, out var task)) task.Cancel();
    }

    private Task<TResult> Queue<TResult>(Func<TResult> action, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<TResult>(cancellationToken);
        if (Volatile.Read(ref _quiescing) != 0 || _host == null || _host->QueueTask == null)
            return Task.FromException<TResult>(new InvalidOperationException("The runtime is shutting down."));

        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var taskId = unchecked((ulong)Interlocked.Increment(ref _nextTaskId));
        var item = new WorkItem<TResult>(action, completion, cancellationToken);
        if (!_tasks.TryAdd(taskId, item))
            return Task.FromException<TResult>(new InvalidOperationException("Could not allocate a managed task ID."));
        item.RegisterCancellation(() =>
        {
            if (_tasks.TryRemove(taskId, out var removed)) removed.Cancel();
        });
        var result = (F4ForgeResult)_host->QueueTask(_context, _runtime, taskId);
        if (result != F4ForgeResult.Success && _tasks.TryRemove(taskId, out var rejected))
            rejected.Reject(result);
        return completion.Task;
    }

    private abstract class WorkItem
    {
        internal abstract void Execute();
        internal abstract void Cancel();
        internal abstract void Reject(F4ForgeResult result);
    }

    private sealed class WorkItem<TResult> : WorkItem
    {
        private readonly Func<TResult> _action;
        private readonly TaskCompletionSource<TResult> _completion;
        private readonly CancellationToken _cancellationToken;
        private CancellationTokenRegistration _registration;

        internal WorkItem(Func<TResult> action, TaskCompletionSource<TResult> completion, CancellationToken cancellationToken)
        {
            _action = action;
            _completion = completion;
            _cancellationToken = cancellationToken;
        }

        internal void RegisterCancellation(Action callback) =>
            _registration = _cancellationToken.Register(callback);

        internal override void Execute()
        {
            _registration.Dispose();
            try { _completion.TrySetResult(_action()); }
            catch (Exception exception) { _completion.TrySetException(exception); }
        }

        internal override void Cancel()
        {
            _registration.Dispose();
            _completion.TrySetCanceled(_cancellationToken.IsCancellationRequested ? _cancellationToken : new CancellationToken(true));
        }

        internal override void Reject(F4ForgeResult result)
        {
            _registration.Dispose();
            _completion.TrySetException(new InvalidOperationException($"Native scheduler rejected the task: {result}."));
        }
    }
}
