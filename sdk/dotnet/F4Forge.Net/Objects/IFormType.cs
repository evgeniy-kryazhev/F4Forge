namespace F4Forge.Net.Objects;

/// <summary>Defines type checks and construction for built-in and external form types.</summary>
/// <typeparam name="TSelf">The form type created by this contract.</typeparam>
public interface IFormType<TSelf> where TSelf : Form
{
    /// <summary>Checks the copied native description without accessing game memory.</summary>
    /// <param name="snapshot">The description captured when the form was resolved.</param>
    /// <returns>Whether the description matches this form type.</returns>
    static abstract bool Matches(FormSnapshot snapshot);

    /// <summary>Creates a typed wrapper retaining the original identity and mod scope.</summary>
    /// <param name="form">The form whose identity and channel must be retained.</param>
    /// <returns>A wrapper of this form type.</returns>
    static abstract TSelf Create(Form form);
}
