namespace F4Forge.DotNet.Sdk;

public readonly record struct EndpointHandle(ulong Value)
{
    public bool IsValid => Value != 0;
}

public readonly record struct EventSubscriptionHandle(ulong Value)
{
    public bool IsValid => Value != 0;
}

public readonly record struct InterceptorSubscriptionHandle(ulong Value)
{
    public bool IsValid => Value != 0;
}

public enum F4ForgeResult
{
    Success = 0,
    InvalidArgument = 1,
    InvalidAbiVersion = 2,
    InvalidStructSize = 3,
    InvalidHandle = 4,
    StaleHandle = 5,
    InactiveEndpoint = 6,
    InactiveModule = 7,
    InactiveRuntime = 8,
    InactivePlugin = 9,
    InvalidRequestSize = 10,
    InvalidResponseCapacity = 11,
    BufferTooSmall = 12,
    WrongThread = 13,
    CapabilityUnavailable = 14,
    NotFound = 15,
    AlreadyRegistered = 16,
    CallbackExpired = 17,
    RuntimeUnavailable = 18,
    InternalError = 19
}
