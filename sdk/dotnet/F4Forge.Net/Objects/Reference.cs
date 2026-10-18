namespace F4Forge.Net.Objects;

/// <summary>A world-reference view over a scoped form identity.</summary>
public class Reference : Form, IFormView<Reference>
{
    /// <summary>Copies a compatible form identity.</summary>
    protected Reference(Form source) : base(source)
    {
        if (!source.Snapshot.IsReference)
        {
            throw new InvalidCastException("The supplied form is not a world reference.");
        }
    }

    /// <inheritdoc />
    public static bool Matches(FormSnapshot snapshot) => snapshot.IsReference;

    /// <inheritdoc />
    public static Reference Create(Form form) => new(form);

    /// <summary>Reads position, base definition, cell and location in one native call.</summary>
    public ReferenceSnapshot GetSnapshot()
    {
        return Messages.Call<ReferenceSnapshot>("forms.reference", new { FormId, Session });
    }
}
