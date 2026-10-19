namespace F4Forge.Net.Objects.Queries;

/// <summary>Resolves an editor ID retained in the game's runtime index.</summary>
/// <typeparam name="TForm">The expected form view.</typeparam>
/// <param name="EditorId">Editor ID to resolve; unavailable IDs return null.</param>
public sealed record FormByEditorId<TForm>(string EditorId) : IFormQuery<TForm>
    where TForm : Form, IFormView<TForm>
{
    /// <inheritdoc />
    public string MethodName => "forms.resolve";

    /// <inheritdoc />
    public object Arguments
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(EditorId);
            return new { EditorId };
        }
    }
}
