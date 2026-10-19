using F4Forge.Net.Messaging;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Objects.Operations;

/// <summary>A reusable ownership check for operations on a resolved form.</summary>
/// <param name="Form">The scoped identity; native operations validate its session and current existence.</param>
public abstract record FormOperation(Form Form) : IOperation
{
    /// <inheritdoc />
    public abstract string MethodName { get; }

    /// <inheritdoc />
    public abstract object? Arguments { get; }

    /// <inheritdoc />
    public virtual void Validate(IMessageChannel messages)
    {
        ArgumentNullException.ThrowIfNull(Form);
        if (!ReferenceEquals(Form.Messages, messages))
        {
            throw new ArgumentException("The form must belong to the channel's mod scope.", nameof(messages));
        }
    }
}
