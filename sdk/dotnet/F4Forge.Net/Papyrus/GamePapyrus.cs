using F4Forge.Net.Events;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Objects;

namespace F4Forge.Net.Papyrus;

/// <summary>Dynamic VM calls with no per-function SDK bindings. All engine submission is queued.</summary>
public sealed partial class GamePapyrus : IDisposable
{
    private readonly IMessageChannel _messages;
    private readonly IMessageChannel _cleanup;
    private readonly IEventChannel _events;
    private readonly ModLifetime _lifetime;
    private readonly string _scope = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stopping = new();
    private int _closed;
    private int _used;

    internal GamePapyrus(IMessageChannel messages, IMessageChannel cleanup, IEventChannel events, ModLifetime lifetime)
    {
        _messages = messages;
        _cleanup = cleanup;
        _events = events;
        _lifetime = lifetime;
        lifetime.Own(this);
    }

    /// <summary>Describes a form-backed script without resolving native memory on this thread.</summary>
    /// <param name="form">Form in this mod's scope.</param>
    /// <param name="scriptName">Attached script name.</param>
    /// <returns>An identity validated by the VM when an operation executes.</returns>
    public PapyrusObject Object(Form form, string scriptName)
    {
        ArgumentNullException.ThrowIfNull(form);
        ValidateName(scriptName);
        _lifetime.ThrowIfClosed();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        if (!ReferenceEquals(form.Messages, _messages))
        {
            throw new ArgumentException("The form belongs to another mod scope.", nameof(form));
        }
        return new(form.FormId, form.Session, scriptName);
    }

    /// <summary>Calls a global Papyrus function, including native functions registered by F4SE or other plugins.</summary>
    /// <param name="scriptName">Script type name.</param>
    /// <param name="functionName">Function name.</param>
    /// <param name="arguments">Values converted according to the VM signature; omitted arguments use VM defaults.</param>
    /// <param name="cancellationToken">Cancels waiting; an already dispatched VM call is not aborted.</param>
    /// <returns>The copied dynamic result. Calls time out after 30 seconds.</returns>
    public Task<PapyrusValue> CallStaticAsync(string scriptName, string functionName,
        object?[]? arguments = null, CancellationToken cancellationToken = default)
    {
        return InvokeAsync("static", scriptName, functionName, null, arguments, cancellationToken);
    }

    /// <summary>Calls a method on an attached or previously returned script object.</summary>
    /// <param name="target">Script identity in this client's scope and session.</param>
    /// <param name="functionName">Method name, resolved in the script's current state.</param>
    /// <param name="arguments">Arguments matching VM metadata.</param>
    /// <param name="cancellationToken">Cancels waiting without interrupting an executing script.</param>
    /// <returns>The copied result, within 30 seconds.</returns>
    public Task<PapyrusValue> CallAsync(PapyrusObject target, string functionName,
        object?[]? arguments = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return InvokeAsync("method", target.ScriptName, functionName, target, arguments, cancellationToken);
    }

    /// <summary>Reads a property through the VM, including scripted getters.</summary>
    /// <param name="target">Script identity.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="cancellationToken">Cancels waiting.</param>
    /// <returns>The copied property value, within 30 seconds.</returns>
    public Task<PapyrusValue> GetPropertyAsync(PapyrusObject target, string propertyName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return InvokeAsync("get", target.ScriptName, propertyName, target, null, cancellationToken);
    }

    /// <summary>Writes a property through the VM, preserving scripted setter behavior.</summary>
    /// <param name="target">Script identity.</param>
    /// <param name="propertyName">Property name.</param>
    /// <param name="value">Value matching the property's metadata.</param>
    /// <param name="cancellationToken">Cancels waiting.</param>
    /// <returns>The VM completion task, within 30 seconds.</returns>
    public Task<PapyrusValue> SetPropertyAsync(PapyrusObject target, string propertyName, object? value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return InvokeAsync("set", target.ScriptName, propertyName, target, [value], cancellationToken);
    }

    private Task<PapyrusValue> InvokeAsync(string operation, string scriptName, string name,
        PapyrusObject? target, object?[]? arguments, CancellationToken cancellationToken)
    {
        ValidateName(scriptName);
        ValidateName(name);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        var values = (arguments ?? []).Select(value => PapyrusValue.From(value, _messages)).ToArray();
        return InvokeTrackedAsync(operation, scriptName, name, target, values, cancellationToken);
    }

    private async Task<PapyrusValue> InvokeTrackedAsync(string operation, string scriptName, string name,
        PapyrusObject? target, PapyrusValue[] arguments, CancellationToken cancellationToken)
    {
        using var invocation = _lifetime.Enter();
        return await InvokeCoreAsync(operation, scriptName, name, target, arguments, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PapyrusValue> InvokeCoreAsync(string operation, string scriptName, string name,
        PapyrusObject? target, PapyrusValue[] arguments, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token, _stopping.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<PapyrusValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = _events.Subscribe<Completion>("papyrus.completed", result =>
        {
            if (result.RequestId != id)
            {
                return;
            }
            if (result.Success)
            {
                completion.TrySetResult(result.Value ?? new PapyrusValue());
            }
            else
            {
                completion.TrySetException(new MessageException("papyrus_failed", result.Error ?? "Papyrus operation failed."));
            }
        });
        var submitted = false;
        try
        {
            if (Interlocked.CompareExchange(ref _used, 1, 0) == 2 || Volatile.Read(ref _closed) != 0)
            {
                throw new ObjectDisposedException(nameof(GamePapyrus));
            }
            _messages.Call<bool>("papyrus.begin", new
            {
                Scope = _scope, RequestId = id, Operation = operation, ScriptName = scriptName,
                Name = name, Target = target, Arguments = arguments
            });
            submitted = true;
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            // Stop releases the entire native scope synchronously. Async cleanup
            // must not access its transport after that barrier.
            if (submitted && Volatile.Read(ref _closed) == 0 && !_lifetime.Token.IsCancellationRequested)
            {
                try
                {
                    _messages.Call<bool>("papyrus.forget", new { Scope = _scope, RequestId = id });
                }
                catch (ObjectDisposedException)
                {
                    // Concurrent scope shutdown already owns native cleanup.
                }
            }
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256 || name.Contains('\0'))
        {
            throw new ArgumentException("A Papyrus name must contain at most 256 characters and no NUL.", nameof(name));
        }
    }

    /// <summary>Synchronously releases native requests and script identities owned by this client.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }
        _stopping.Cancel();
        var previouslyUsed = Interlocked.Exchange(ref _used, 2);
        if (previouslyUsed == 1)
        {
            _cleanup.Call<bool>("papyrus.release", new { Scope = _scope });
        }
    }

    private sealed record Completion(string RequestId, bool Success, PapyrusValue? Value, string? Error);
}
