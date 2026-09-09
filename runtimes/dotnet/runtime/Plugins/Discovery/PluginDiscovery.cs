using System.Text.Json;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins.Discovery;

internal static class PluginDiscovery
{
    public static (List<PluginCandidate> Candidates, string[] LegacyPaths) Discover(
        string directory, bool allowManifestlessPlugins, string frameworkVersion)
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
                var manifest = ReadManifest(manifestPath);
                if (manifest == null || !ValidateManifest(manifest, manifestPath, frameworkVersion)) continue;
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

    private static PluginManifest? ReadManifest(string path)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path));
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            Logger.Error($"Invalid plugin manifest: {path}");
            return null;
        }
        if (!string.IsNullOrWhiteSpace(manifest.Runtime) &&
            !manifest.Runtime.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return null;
        return manifest;
    }

    private static bool ValidateManifest(PluginManifest manifest, string path, string frameworkVersion)
    {
        var id = string.IsNullOrWhiteSpace(manifest.Id) ? path : manifest.Id;
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            Logger.Error($"Plugin manifest must declare an id: {path}.");
            return false;
        }
        if (!TryParseVersion(manifest.Version, out _))
        {
            Logger.Error($"Plugin '{id}' has malformed version in {path}.");
            return false;
        }
        if (!TryParseVersion(manifest.MinimumF4ForgeVersion, out var minimum))
        {
            Logger.Error($"Plugin '{id}' has malformed minimumF4ForgeVersion in {path}.");
            return false;
        }
        if (minimum > new Version(frameworkVersion))
        {
            Logger.Error($"Plugin '{id}' requires F4Forge {minimum}, current framework version is {frameworkVersion}.");
            return false;
        }
        return true;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            version = new Version(0, 0);
            return true;
        }
        return Version.TryParse(value, out version!);
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
