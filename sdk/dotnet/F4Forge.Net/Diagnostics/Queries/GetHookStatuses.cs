using F4Forge.Net.Operations;

namespace F4Forge.Net.Diagnostics.Queries;

/// <summary>Reads statuses of hooks registered with native diagnostics.</summary>
public sealed record GetHookStatuses : IQuery<HookStatus[]>
{
    /// <inheritdoc />
    public string MethodName => "diagnostics.hooks";

    /// <inheritdoc />
    public object? Arguments => null;
}
