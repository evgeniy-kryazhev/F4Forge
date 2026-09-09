using System.Text.Json.Serialization;

namespace F4Forge.DotNet.Runtime.Plugins.Discovery;

internal sealed class PluginManifest
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("entryAssembly")] public string? EntryAssembly { get; set; }
    [JsonPropertyName("runtime")] public string? Runtime { get; set; }
    [JsonPropertyName("minimumF4ForgeVersion")] public string? MinimumF4ForgeVersion { get; set; }
    [JsonPropertyName("dependencies")] public string[]? Dependencies { get; set; }
    [JsonPropertyName("providedCapabilities")] public string[]? ProvidedCapabilities { get; set; }
}
