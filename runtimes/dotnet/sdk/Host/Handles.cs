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

public readonly record struct AsyncOperationHandle(ulong Value)
{
    public bool IsValid => Value != 0;
}

public readonly record struct ModuleHandle(ulong Value)
{
    public static ModuleHandle Invalid => new(0);
    public bool IsValid => Value != 0;
}

public enum F4ForgeResult : uint
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
    InternalError = 19,
    Timeout = 20,
    NotReady = 21,
    OperationCancelled = 22,
    OperationBusy = 23,
    NotCancellable = 24,
    WouldDeadlock = 25,
    SchedulerUnavailable = 26
}

public enum F4ForgeAsyncOperationState : uint
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Cancelled = 3
}

public enum F4ForgeLogLevel : uint
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}
