namespace F4Forge.Net.Objects;

/// <summary>A world-reference identity scoped to one mod and game session.</summary>
public class Reference : Form, IFormType<Reference>
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
    public new static bool Matches(FormSnapshot snapshot) => snapshot.IsReference;

    /// <inheritdoc />
    public new static Reference Create(Form form) => new(form);
}
