using F4Forge.Net.Messaging;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Diagnostics.Queries;

/// <summary>Reads a mod's newest operations from the bounded native history.</summary>
/// <param name="ModId">Absolute mod DLL path.</param>
/// <param name="Maximum">Maximum count, from 1 to 256.</param>
public sealed record GetDiagnosticOperations(string ModId, int Maximum = 64) : IQuery<DiagnosticRecord[]>
{
    /// <inheritdoc />
    public string MethodName => "diagnostics.operations";

    /// <inheritdoc />
    public object Arguments => new { ModId, Maximum };

    /// <inheritdoc />
    public void Validate(IMessageChannel messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ModId);
        if (Maximum is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(Maximum));
        }
    }
}
