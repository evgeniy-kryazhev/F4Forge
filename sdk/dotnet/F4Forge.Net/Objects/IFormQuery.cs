using F4Forge.Net.Operations;

namespace F4Forge.Net.Objects;

/// <summary>Describes a native lookup and the form type expected from its response.</summary>
/// <typeparam name="TForm">The form type constructed from the copied native description.</typeparam>
public interface IFormQuery<TForm> : IQuery<FormSnapshot?> where TForm : Form, IFormType<TForm>;
