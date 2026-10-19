namespace F4Forge.Net.Operations;

/// <summary>A synchronous state-changing operation with an explicit response contract.</summary>
/// <typeparam name="TResult">The native acknowledgement or result, including false acknowledgements.</typeparam>
public interface ICommand<TResult> : IOperation;
