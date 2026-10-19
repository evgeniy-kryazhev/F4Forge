namespace F4Forge.Net.Operations;

/// <summary>A synchronous request for copied data through the shared serializer.</summary>
/// <typeparam name="TResult">The response contract returned by the native handler.</typeparam>
public interface IQuery<TResult> : IOperation;
