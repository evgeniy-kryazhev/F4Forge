namespace F4Forge.Net.Diagnostics;

/// <summary>A completed operation. Records retain strings and values, never mod objects or exceptions.</summary>
/// <param name="Id">Unique operation identifier.</param>
/// <param name="ParentId">Enclosing operation, or an empty string.</param>
/// <param name="ModId">Absolute path identifying the mod DLL.</param>
/// <param name="Kind">Operation category.</param>
/// <param name="Name">Method, event or lifecycle stage.</param>
/// <param name="StartedAt">UTC start time.</param>
/// <param name="ThreadId">Managed source thread.</param>
/// <param name="ElapsedMilliseconds">Elapsed wall time.</param>
/// <param name="Success">Whether the operation completed successfully.</param>
/// <param name="ExceptionType">Exception type name, or an empty string.</param>
/// <param name="Message">Failure message, or an empty string.</param>
/// <param name="StackTrace">Managed exception details, or an empty string.</param>
public sealed record DiagnosticRecord(string Id, string ParentId, string ModId, string Kind, string Name,
    DateTimeOffset StartedAt, int ThreadId, double ElapsedMilliseconds, bool Success,
    string ExceptionType, string Message, string StackTrace);

/// <summary>Status of one native hook; an active hook is not proof of in-game correctness.</summary>
/// <param name="Name">Unique hook name.</param>
/// <param name="Kind">Installation mechanism.</param>
/// <param name="State">Checking, active, inactive or failed, in lowercase.</param>
/// <param name="Detail">Failure or lifecycle details.</param>
/// <param name="UpdatedAtUnixMilliseconds">UTC update time.</param>
public sealed record HookStatus(string Name, string Kind, string State, string Detail, long UpdatedAtUnixMilliseconds);
