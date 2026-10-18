using System.Diagnostics;
using F4Forge.Net.Messaging;

namespace F4Forge.Net.Diagnostics;

internal static class DiagnosticTrace
{
    private static readonly AsyncLocal<string?> Owner = new();
    private static readonly AsyncLocal<string?> Operation = new();

    internal static string? CurrentOwner => Owner.Value;

    internal static IDisposable Bind(string modId)
    {
        var previous = Owner.Value;
        Owner.Value = modId;
        return new RestoreOwner(previous);
    }

    internal static T Run<T>(string? modId, string kind, string name, Action<DiagnosticRecord> sink, Func<T> action)
    {
        if (modId is null)
        {
            return action();
        }
        var parent = Operation.Value;
        var identifier = Guid.NewGuid().ToString("N");
        var started = DateTimeOffset.UtcNow;
        var timestamp = Stopwatch.GetTimestamp();
        var thread = Environment.CurrentManagedThreadId;
        var exceptionType = string.Empty;
        var message = string.Empty;
        var stack = string.Empty;
        var success = false;
        using var ownership = Bind(modId);
        Operation.Value = identifier;
        try
        {
            var result = action();
            success = true;
            return result;
        }
        catch (Exception exception)
        {
            exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
            message = exception.Message;
            stack = exception.ToString();
            throw;
        }
        finally
        {
            Operation.Value = parent;
            try
            {
                sink(new DiagnosticRecord(identifier, parent ?? string.Empty, modId, kind, name, started, thread,
                    Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds, success,
                    Limit(exceptionType, 256), Limit(message, 2048), Limit(stack, 8192)));
            }
            catch
            {
                // Missing diagnostics (including test transports) or a failing
                // serializer must never change the outcome of the original call.
            }
        }
    }

    internal static T Run<T>(string modId, string kind, string name, IMessageChannel messages, Func<T> action)
    {
        return Run(modId, kind, name, entry => messages.Call<bool>("diagnostics.record", entry), action);
    }

    private static string Limit(string value, int maximum)
    {
        if (value.Length <= maximum)
        {
            return value;
        }
        // Avoid cutting a UTF-16 surrogate pair when truncating diagnostic text.
        var length = char.IsHighSurrogate(value[maximum - 1]) ? maximum - 1 : maximum;
        return value[..length];
    }

    private sealed class RestoreOwner(string? previous) : IDisposable
    {
        public void Dispose()
        {
            Owner.Value = previous;
        }
    }
}
