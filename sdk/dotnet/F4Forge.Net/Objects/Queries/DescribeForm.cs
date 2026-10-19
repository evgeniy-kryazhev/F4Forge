using F4Forge.Net.Objects.Operations;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Objects.Queries;

/// <summary>Reads a fresh description, or null if the scoped form no longer exists.</summary>
/// <param name="Form">The form to resolve again, including its session generation.</param>
public sealed record DescribeForm(Form Form) : FormOperation(Form), IQuery<FormSnapshot?>
{
    /// <inheritdoc />
    public override string MethodName => "forms.describe";

    /// <inheritdoc />
    public override object Arguments => new { Form.FormId, Form.Session };
}
