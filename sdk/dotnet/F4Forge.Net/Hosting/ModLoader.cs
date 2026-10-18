using System.Reflection;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Events;
using F4Forge.Net.Serialization;

namespace F4Forge.Net.Hosting;

internal sealed class ModLoader(IMessageChannel messages, IEventChannel events, IDataSerializer serializer,
    Action<string> reportError) : IDisposable
{
    private readonly List<LoadedMod> _mods = [];

    public int Count => _mods.Count;

    public void LoadDirectory(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var assemblyPaths = System.IO.Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase);
        foreach (var assemblyPath in assemblyPaths)
        {
            try
            {
                // Native dependencies are also DLLs, but cannot contain a managed mod.
                AssemblyName assemblyName;
                try
                {
                    assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                }
                catch (BadImageFormatException)
                {
                    continue;
                }
                if (assemblyName.Name == typeof(IMod).Assembly.GetName().Name)
                {
                    continue;
                }
                LoadMod(Path.GetFullPath(assemblyPath));
            }
            catch (Exception exception)
            {
                reportError($"{assemblyPath}: {exception}");
            }
        }
    }

    private void LoadMod(string assemblyPath)
    {
        var directory = Path.GetDirectoryName(assemblyPath)!;
        var loadContext = new ModLoadContext(assemblyPath);
        var lifetime = new ModLifetime();
        var scope = new ScopedChannel(messages, lifetime);
        var eventScope = new ScopedEvents(events, lifetime);
        IMod? mod = null;
        try
        {
            // Keep the mod's DLL replaceable; the collectible context owns its loaded image.
            using var assemblyStream = File.OpenRead(assemblyPath);
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var entryPoints = assembly.GetExportedTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IMod).IsAssignableFrom(type))
                .ToArray();
            if (entryPoints.Length == 0)
            {
                // A managed dependency is not a mod. Release its discovery context.
                Release(new LoadedMod(null, scope, eventScope, loadContext));
                return;
            }
            if (entryPoints.Length != 1)
            {
                throw new InvalidDataException("A mod assembly must contain exactly one public IMod implementation.");
            }
            mod = (IMod)Activator.CreateInstance(entryPoints[0])!;
            mod.Start(new ModContext(directory, scope, eventScope, serializer));
            _mods.Add(new LoadedMod(mod, scope, eventScope, loadContext));
        }
        catch
        {
            Release(new LoadedMod(mod, scope, eventScope, loadContext));
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var mod in _mods.AsEnumerable().Reverse())
        {
            Release(mod);
        }
        _mods.Clear();
    }

    private void Release(LoadedMod mod)
    {
        mod.Events.Dispose();
        mod.Messages.Dispose();
        try
        {
            mod.Instance?.Dispose();
        }
        catch (Exception exception)
        {
            reportError($"Mod cleanup failed: {exception}");
        }
        finally
        {
            mod.Context.Unload();
        }
    }

    private sealed record LoadedMod(IMod? Instance, ScopedChannel Messages, ScopedEvents Events, ModLoadContext Context);
}
