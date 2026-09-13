using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins.Discovery;

internal sealed class PluginDiscovery(PluginManifestValidator validator)
{
    public (List<PluginCandidate> Candidates, string[] LegacyPaths) Discover(
        string directory, bool allowManifestlessPlugins)
    {
        if (!Directory.Exists(directory))
        {
            Logger.Warning($"Managed plugin directory does not exist: {directory}");
            return ([], []);
        }

        var candidates = new List<PluginCandidate>();
        foreach (var pluginDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(pluginDirectory, "f4forge.plugin.json");
            if (!File.Exists(manifestPath)) continue;
            try
            {
                var manifest = validator.ReadAndValidate(manifestPath);
                if (manifest == null) continue;
                var root = Path.GetFullPath(pluginDirectory);
                var assemblyPath = Path.GetFullPath(Path.Combine(root, manifest.EntryAssembly!));
                if (!IsWithinDirectory(root, assemblyPath) || !File.Exists(assemblyPath))
                {
                    Logger.Error($"Plugin entry assembly is outside its directory or missing: {manifestPath}");
                    continue;
                }
                candidates.Add(new PluginCandidate(assemblyPath, manifest));
            }
            catch (Exception exception)
            {
                Logger.Error($"Plugin manifest failed: {manifestPath}: {exception}");
            }
        }

        var legacyPaths = Directory.EnumerateFiles(directory, "*.dll")
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!allowManifestlessPlugins && legacyPaths.Length != 0)
            Logger.Warning($"Ignoring {legacyPaths.Length} manifestless plugin DLL(s); legacy mode is disabled.");
        Logger.Info($"Managed plugin DLLs discovered: {legacyPaths.Length}");
        return (candidates, allowManifestlessPlugins ? legacyPaths : []);
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
