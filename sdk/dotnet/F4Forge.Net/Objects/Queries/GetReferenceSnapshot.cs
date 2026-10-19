using F4Forge.Net.Messaging;
using F4Forge.Net.Objects.Operations;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Objects.Queries;

/// <summary>Reads position, base definition, cell and location in one native call.</summary>
/// <param name="Reference">The world reference to resolve again.</param>
public sealed record GetReferenceSnapshot(Reference Reference) : FormOperation(Reference), IQuery<ReferenceSnapshot>
{
    /// <inheritdoc />
    public override string MethodName => "forms.reference";

    /// <inheritdoc />
    public override object Arguments => new { Reference.FormId, Reference.Session };

    /// <inheritdoc />
    public override void Validate(IMessageChannel messages)
    {
        base.Validate(messages);
        // A record copy must not redirect the wire target after ownership validation.
        if (!ReferenceEquals(Reference, Form))
        {
            throw new ArgumentException("The reference must match the operation's form identity.");
        }
    }
}
