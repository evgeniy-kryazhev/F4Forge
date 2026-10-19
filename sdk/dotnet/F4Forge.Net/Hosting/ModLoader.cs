using System.Reflection;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Events;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Serialization;

namespace F4Forge.Net.Hosting;

internal sealed class ModLoader(IMessageChannel messages, IEventChannel events, IDataSerializer serializer,
    Action<string> reportError, Action<string>? managedLog = null) : IDisposable, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly List<LoadedMod> _mods = [];
    private Task _completion = Task.CompletedTask;
    private bool _stopping;

    internal enum ModState { Starting, Running, Stopping, Failed, Stopped }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _mods.Count(mod => mod.State == ModState.Running);
            }
        }
    }

    internal Task Completion
    {
        get
        {
            lock (_gate)
            {
                return _completion;
            }
        }
    }

    public void LoadDirectory(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var paths = System.IO.Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                AssemblyName name;
                try
                {
                    name = AssemblyName.GetAssemblyName(path);
                }
                catch (BadImageFormatException)
                {
                    continue;
                }
                if (name.Name == typeof(IMod).Assembly.GetName().Name)
                {
                    continue;
                }
                LoadMod(Path.GetFullPath(path));
            }
            catch (Exception exception)
            {
                Report($"{path}: {exception}");
            }
        }
    }

    private void LoadMod(string path)
    {
        var context = new ModLoadContext(path);
        var lifetime = new ModLifetime(error => Report($"Mod '{path}': {error}"));
        using var discovery = lifetime.Enter();
        var scope = new ScopedChannel(messages, lifetime);
        var mod = new LoadedMod(path, context, lifetime, scope);
        mod.Events = new ScopedEvents(events, lifetime, entry => Record(mod, entry), Report);
        lock (_gate)
        {
            if (_stopping)
            {
                context.Unload();
                throw new ObjectDisposedException(nameof(ModLoader));
            }
            _mods.Add(mod);
        }
        try
        {
            using var stream = File.OpenRead(path);
            var assembly = context.LoadFromStream(stream);
            var entries = assembly.GetExportedTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IMod).IsAssignableFrom(type)).ToArray();
            if (entries.Length == 0)
            {
                Stop(mod);
                return;
            }
            if (entries.Length != 1)
            {
                throw new InvalidDataException("A mod assembly must contain exactly one public IMod implementation.");
            }
            // Each startup runs immediately up to its first incomplete await.
            // Its task is reserved before constructors or mod code can reenter stop.
            _ = StartAsync(mod, entries[0]);
        }
        catch
        {
            mod.State = ModState.Failed;
            Stop(mod);
            throw;
        }
    }

    private async Task StartAsync(LoadedMod mod, Type entry)
    {
        try
        {
            await mod.Lifetime.Run(() => TraceStartupAsync(mod, entry)).ConfigureAwait(false);
            lock (_gate)
            {
                if (mod.State == ModState.Starting)
                {
                    mod.State = ModState.Running;
                }
            }
        }
        catch (OperationCanceledException) when (mod.Lifetime.Token.IsCancellationRequested)
        {
            Stop(mod);
        }
        catch (ObjectDisposedException) when (mod.Lifetime.Token.IsCancellationRequested)
        {
            Stop(mod);
        }
        catch (Exception exception)
        {
            Report($"{mod.Path}: {exception}");
            mod.State = ModState.Failed;
            Stop(mod);
        }
    }

    private Task TraceStartupAsync(LoadedMod mod, Type entry)
    {
        return DiagnosticTrace.RunAsync(mod.Path, "mod.load", Path.GetFileName(mod.Path), record => Record(mod, record),
            () => DiagnosticTrace.RunAsync(mod.Path, "mod.start", entry.FullName ?? mod.Path,
                record => Record(mod, record), () => StartInstanceAsync(mod, entry), mod.Lifetime.Token),
            mod.Lifetime.Token);
    }

    private async Task StartInstanceAsync(LoadedMod mod, Type entry)
    {
        mod.Instance = (IMod)Activator.CreateInstance(entry)!;
        var context = new ModContext(Path.GetDirectoryName(mod.Path)!, mod.Messages, mod.Events!,
            serializer, mod.Path, mod.Lifetime);
        await mod.Instance.StartAsync(context, mod.Lifetime.Token).ConfigureAwait(false);
    }

    private void Record(LoadedMod mod, DiagnosticRecord entry)
    {
        try
        {
            // Closing a mod revokes every native path, including diagnostics.
            // Async cleanup must remain safe even if the host is already gone.
            mod.Messages.Call<bool>("diagnostics.record", entry);
        }
        catch (ObjectDisposedException)
        {
            (managedLog ?? Console.Error.WriteLine)($"Mod '{mod.Path}', {entry.Kind}/{entry.Name}: {entry.Message}");
        }
        catch
        {
            // Diagnostics are optional in isolated transports.
        }
    }

    private void Report(string message)
    {
        try
        {
            reportError(message);
        }
        catch
        {
            // Logging cannot break cancellation, cleanup, or a native callback.
        }
    }

    private void Stop(LoadedMod mod)
    {
        lock (mod.Gate)
        {
            if (mod.Cleanup is not null)
            {
                return;
            }
            lock (_gate)
            {
                mod.State = ModState.Stopping;
            }
            // Install completion before closing: a cancellation callback can reenter.
            mod.Cleanup = mod.Finished.Task;
        }
        try
        {
            mod.Lifetime.Close();
        }
        catch (Exception exception)
        {
            Report($"{mod.Path}: cancellation cleanup failed: {exception}");
        }
        Detach(mod, "events", () => mod.Events?.Dispose());
        Detach(mod, "messages", mod.Messages.Dispose);
        _ = FinishAsync(mod);
    }

    private void Detach(LoadedMod mod, string stage, Action action)
    {
        try
        {
            DiagnosticTrace.Run(mod.Path, "mod.stop", stage, entry => Record(mod, entry), () =>
            {
                action();
                return true;
            });
        }
        catch (Exception exception)
        {
            Report($"{mod.Path}: cleanup failed ({stage}): {exception}");
        }
    }

    private async Task FinishAsync(LoadedMod mod)
    {
        try
        {
            // DisposeAsync belongs to mod code and may have a slow synchronous
            // prefix. Only registration detachment runs in the native stop call.
            var cleanup = Task.Run(() => ReleaseAsync(mod));
            if (!cleanup.IsCompleted && await Task.WhenAny(cleanup, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false) != cleanup)
            {
                Report($"Mod '{mod.Path}' is still stopping after five seconds; its assembly remains retained.");
            }
            await cleanup.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Report($"{mod.Path}: asynchronous cleanup failed: {exception}");
        }
        finally
        {
            lock (_gate)
            {
                mod.State = ModState.Stopped;
                _mods.Remove(mod);
            }
            mod.Finished.TrySetResult();
        }
    }

    private async Task ReleaseAsync(LoadedMod mod)
    {
        await mod.Lifetime.Completion.ConfigureAwait(false);
        try
        {
            await DiagnosticTrace.RunAsync(mod.Path, "mod.stop", "instance", entry => Record(mod, entry), async () =>
            {
                if (mod.Instance is not null)
                {
                    await mod.Instance.DisposeAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            mod.Instance = null;
            Detach(mod, "assembly", () =>
            {
                var context = mod.Context;
                mod.Context = null;
                context?.Unload();
            });
        }
    }

    public void Dispose()
    {
        LoadedMod[] mods;
        lock (_gate)
        {
            if (_stopping)
            {
                return;
            }
            _stopping = true;
            mods = _mods.ToArray();
            _completion = Task.WhenAll(mods.Select(mod => mod.Finished.Task));
        }
        foreach (var mod in mods.Reverse())
        {
            Stop(mod);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Completion.ConfigureAwait(false);
    }

    private sealed class LoadedMod(string path, ModLoadContext context, ModLifetime lifetime, ScopedChannel messages)
    {
        internal readonly object Gate = new();
        internal readonly TaskCompletionSource Finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string Path { get; } = path;
        internal ModLoadContext? Context { get; set; } = context;
        internal ModLifetime Lifetime { get; } = lifetime;
        internal ScopedChannel Messages { get; } = messages;
        internal ScopedEvents? Events { get; set; }
        internal IMod? Instance { get; set; }
        internal volatile ModState State = ModState.Starting;
        internal Task? Cleanup { get; set; }
    }
}
