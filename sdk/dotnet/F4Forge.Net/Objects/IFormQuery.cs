namespace F4Forge.Net.Objects;

/// <summary>Describes a native lookup and the form view expected from its response.</summary>
/// <typeparam name="TForm">The view constructed from the copied native description.</typeparam>
public interface IFormQuery<TForm> where TForm : Form, IFormView<TForm>
{
    /// <summary>The method registered by the native module.</summary>
    string MethodName { get; }

    /// <summary>Wire arguments passed to the shared serializer, without query metadata.</summary>
    object? Arguments { get; }
}
