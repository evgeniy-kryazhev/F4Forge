using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins.Discovery;

internal sealed class PluginDependencyResolver
{
    private readonly StringComparer _comparer = StringComparer.OrdinalIgnoreCase;

    public List<PluginCandidate> Order(IReadOnlyList<PluginCandidate> candidates)
    {
        var ids = new Dictionary<string, PluginCandidate>(_comparer);
        var capabilities = new Dictionary<string, PluginCandidate>(_comparer);
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Manifest.Id) && !ids.TryAdd(candidate.Manifest.Id!, candidate))
                Logger.Error($"Duplicate managed plugin id: {candidate.Manifest.Id}");
            foreach (var capability in candidate.Manifest.ProvidedCapabilities ?? [])
                if (!capabilities.TryAdd(capability, candidate))
                    Logger.Error($"Duplicate managed capability provider: {capability}");
        }

        if (ids.Count != candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.Manifest.Id)) ||
            capabilities.Count != candidates.SelectMany(candidate => candidate.Manifest.ProvidedCapabilities ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()) return [];

        var providers = new Dictionary<string, PluginCandidate>(ids, _comparer);
        foreach (var capability in capabilities)
            if (!providers.TryAdd(capability.Key, capability.Value))
            {
                Logger.Error($"Managed plugin id/capability collision: {capability.Key}");
                return [];
            }

        var visiting = new HashSet<PluginCandidate>();
        var visited = new HashSet<PluginCandidate>();
        var failed = new HashSet<PluginCandidate>();
        var ordered = new List<PluginCandidate>();
        bool Visit(PluginCandidate candidate)
        {
            if (visited.Contains(candidate)) return true;
            if (failed.Contains(candidate)) return false;
            if (!visiting.Add(candidate))
            {
                Logger.Error($"Managed plugin dependency cycle includes '{candidate.Manifest.Id}'.");
                failed.Add(candidate);
                return false;
            }
            var valid = true;
            foreach (var dependency in candidate.Manifest.Dependencies ?? [])
            {
                if (!providers.TryGetValue(dependency, out var provider))
                {
                    Logger.Error($"Managed plugin '{candidate.Manifest.Id}' is missing dependency '{dependency}'.");
                    valid = false;
                    break;
                }
                if (!Visit(provider)) { valid = false; break; }
            }
            visiting.Remove(candidate);
            if (!valid) { failed.Add(candidate); return false; }
            visited.Add(candidate);
            ordered.Add(candidate);
            return true;
        }

        foreach (var candidate in candidates) Visit(candidate);
        return ordered;
    }
}
