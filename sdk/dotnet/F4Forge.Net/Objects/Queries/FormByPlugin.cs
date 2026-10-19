using F4Forge.Net.Messaging;

namespace F4Forge.Net.Objects.Queries;

/// <summary>Resolves a plugin-local ID, including light plugins, independently of load order.</summary>
/// <typeparam name="TForm">The expected form view.</typeparam>
/// <param name="Plugin">Plugin filename including its extension.</param>
/// <param name="LocalId">Form ID without its load-order index.</param>
public sealed record FormByPlugin<TForm>(string Plugin, uint LocalId) : IFormQuery<TForm>
    where TForm : Form, IFormView<TForm>
{
    /// <inheritdoc />
    public string MethodName => "forms.resolve";

    /// <inheritdoc />
    public object Arguments => new { Plugin, LocalId };

    /// <inheritdoc />
    public void Validate(IMessageChannel messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Plugin);
    }
}
