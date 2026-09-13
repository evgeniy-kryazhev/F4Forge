using System.Text.Json;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins.Discovery;

internal sealed class PluginManifestValidator(string frameworkVersion)
{
    public PluginManifest? ReadAndValidate(string path)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path));
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            Logger.Error($"Invalid plugin manifest: {path}");
            return null;
        }
        if (!string.IsNullOrWhiteSpace(manifest.Runtime) &&
            !manifest.Runtime.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return null;
        var id = string.IsNullOrWhiteSpace(manifest.Id) ? path : manifest.Id;
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            Logger.Error($"Plugin manifest must declare an id: {path}.");
            return null;
        }
        if (!TryParseVersion(manifest.Version, out _))
        {
            Logger.Error($"Plugin '{id}' has malformed version in {path}.");
            return null;
        }
        if (!TryParseVersion(manifest.MinimumF4ForgeVersion, out var minimum))
        {
            Logger.Error($"Plugin '{id}' has malformed minimumF4ForgeVersion in {path}.");
            return null;
        }
        if (minimum > new Version(frameworkVersion))
        {
            Logger.Error($"Plugin '{id}' requires F4Forge {minimum}, current framework version is {frameworkVersion}.");
            return null;
        }
        return manifest;
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
}
