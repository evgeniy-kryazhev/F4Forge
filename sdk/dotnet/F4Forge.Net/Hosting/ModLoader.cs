using System.Text.Json;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Hosting;

internal sealed class ModLoader(IMessageChannel messages, Action<string> reportError) : IDisposable
{
    private readonly List<LoadedMod> _mods = [];

    public int Count => _mods.Count;

    public void LoadDirectory(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        foreach (var modDirectory in System.IO.Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(modDirectory, "mod.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }
            try
            {
                LoadMod(modDirectory, manifestPath);
            }
            catch (Exception exception)
            {
                reportError($"{modDirectory}: {exception}");
            }
        }
    }

    private void LoadMod(string directory, string manifestPath)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var assemblyName = manifest.RootElement.GetProperty("assembly").GetString();
        if (string.IsNullOrWhiteSpace(assemblyName) || Path.GetFileName(assemblyName) != assemblyName
            || !assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The manifest assembly must be a DLL filename inside the mod directory.");
        }
        var assemblyPath = Path.GetFullPath(Path.Combine(directory, assemblyName));
        var loadContext = new ModLoadContext(assemblyPath);
        var scope = new ScopedChannel(messages);
        IMod? mod = null;
        try
        {
            // Keep the mod's DLL replaceable; the collectible context owns its loaded image.
            using var assemblyStream = File.OpenRead(assemblyPath);
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var entryPoints = assembly.GetExportedTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IMod).IsAssignableFrom(type))
                .ToArray();
            if (entryPoints.Length != 1)
            {
                throw new InvalidDataException("A mod assembly must contain exactly one public IMod implementation.");
            }
            mod = (IMod)Activator.CreateInstance(entryPoints[0])!;
            mod.Start(new ModContext(directory, scope));
            _mods.Add(new LoadedMod(mod, scope, loadContext));
        }
        catch
        {
            Release(new LoadedMod(mod, scope, loadContext));
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

    private sealed record LoadedMod(IMod? Instance, ScopedChannel Messages, ModLoadContext Context);
}
