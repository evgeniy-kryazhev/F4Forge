namespace F4Forge.DotNet.Sdk;

public enum EndpointKind : uint
{
    Method = 1,
    Event = 2,
    Interceptor = 3
}

public enum ThreadPolicy : uint
{
    Any = 0,
    GameOnly = 1
}

public readonly record struct AsyncOperationResult(
    F4ForgeResult WaitResult,
    byte[] Response,
    F4ForgeResult InvocationResult);

public delegate F4ForgeResult EndpointCallback(
    ReadOnlyMemory<byte> request,
    Memory<byte> response);
