namespace F4Forge.Net.Callbacks;

/// <summary>A mutable context valid only during one synchronous callback invocation.</summary>
public abstract class InvocationContext
{
    private int _completed;

    /// <summary>Rejects mutations after the callback has returned or failed.</summary>
    protected void EnsureActive()
    {
        if (Volatile.Read(ref _completed) != 0)
        {
            throw new InvalidOperationException("The synchronous invocation has completed.");
        }
    }

    internal static TResult Invoke<TContext, TResult>(TContext context, Action<TContext> handler,
        Func<TContext, TResult> createResult) where TContext : InvocationContext
    {
        ArgumentNullException.ThrowIfNull(context);
        context.EnsureActive();
        try
        {
            handler(context);
            return createResult(context);
        }
        finally
        {
            // Every adapter has the same completion rule, including handler or
            // result-conversion failures. A retained context cannot be reused.
            Volatile.Write(ref context._completed, 1);
        }
    }
}
