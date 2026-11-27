using System.Reflection;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Hosting;

internal sealed class ModLoader(Action<string> reportError)
{
    private readonly List<IMod> _plugins = [];

    public void LoadDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        var paths = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            try
            {
                LoadAssembly(Path.GetFullPath(path));
            }
            catch (Exception exception)
            {
                reportError($"{path}: {exception}");
            }
        }
    }

    private void LoadAssembly(string path)
    {
        AssemblyName name;
        try
        {
            name = AssemblyName.GetAssemblyName(path);
        }
        catch (BadImageFormatException)
        {
            return; // Native dependencies can be deployed next to the plugin.
        }
        if (AssemblyName.ReferenceMatchesDefinition(name, typeof(IMod).Assembly.GetName()))
        {
            return;
        }

        var context = new ModLoadContext(path);
        var initialized = false;
        try
        {
            using var stream = File.OpenRead(path);
            var assembly = context.LoadFromStream(stream);
            var entries = assembly.GetExportedTypes()
                .Where(type => type.IsClass && !type.IsAbstract && typeof(IMod).IsAssignableFrom(type))
                .ToArray();
            if (entries.Length == 0)
            {
                return;
            }
            if (entries.Length != 1)
            {
                throw new InvalidDataException("A plugin DLL must contain exactly one public IMod implementation.");
            }

            var plugin = (IMod)Activator.CreateInstance(entries[0])!;
            plugin.Initialize(Path.GetDirectoryName(path)!);
            _plugins.Add(plugin);
            initialized = true;
        }
        finally
        {
            if (!initialized)
            {
                context.Unload();
            }
        }
    }
}
