using System.Diagnostics;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Diagnostics;

internal static class DiagnosticTrace
{
    private static readonly AsyncLocal<string?> Owner = new();
    private static readonly AsyncLocal<string?> Operation = new();
    private static readonly AsyncLocal<ModLifetime?> Lifetime = new();

    internal static string? CurrentOwner => Owner.Value;
    internal static ModLifetime? CurrentLifetime => Lifetime.Value;

    internal static IDisposable Bind(string modId)
    {
        return Bind(modId, Lifetime.Value);
    }

    internal static IDisposable Bind(string modId, ModLifetime? lifetime)
    {
        var previous = Owner.Value;
        var previousLifetime = Lifetime.Value;
        Owner.Value = modId;
        Lifetime.Value = lifetime;
        return new RestoreOwner(previous, previousLifetime);
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

    internal static async Task RunAsync(string? modId, string kind, string name,
        Action<DiagnosticRecord> sink, Func<Task> action, CancellationToken cancellationToken = default)
    {
        var parent = Operation.Value;
        var identifier = Guid.NewGuid().ToString("N");
        var started = DateTimeOffset.UtcNow;
        var timestamp = Stopwatch.GetTimestamp();
        var thread = Environment.CurrentManagedThreadId;
        Exception? failure = null;
        using var ownership = modId is null ? null : Bind(modId);
        Operation.Value = identifier;
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            Operation.Value = parent;
            if (modId is not null)
            {
                try
                {
                    sink(new DiagnosticRecord(identifier, parent ?? string.Empty, modId, kind, name,
                        started, thread, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds, failure is null,
                        Limit(failure?.GetType().FullName ?? string.Empty, 256),
                        Limit(failure?.Message ?? string.Empty, 2048), Limit(failure?.ToString() ?? string.Empty, 8192)));
                }
                catch
                {
                    // Diagnostics never replace an operation's result.
                }
            }
        }
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

    private sealed class RestoreOwner(string? previous, ModLifetime? previousLifetime) : IDisposable
    {
        public void Dispose()
        {
            Owner.Value = previous;
            Lifetime.Value = previousLifetime;
        }
    }
}
