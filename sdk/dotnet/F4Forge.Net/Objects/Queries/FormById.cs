namespace F4Forge.Net.Objects.Queries;

/// <summary>Resolves a runtime ID; zero and missing forms return null.</summary>
/// <typeparam name="TForm">The expected form type.</typeparam>
/// <param name="FormId">Runtime form ID in the current game session.</param>
public sealed record FormById<TForm>(uint FormId) : IFormQuery<TForm>
    where TForm : Form, IFormType<TForm>
{
    /// <inheritdoc />
    public string MethodName => "forms.describe";

    /// <inheritdoc />
    public object Arguments => new { FormId };
}
