namespace F4Forge.Net.Interception;

/// <summary>Console decisions layered over the shared, mod-owned interceptor service.</summary>
public static class ConsoleInterceptorExtensions
{
    /// <summary>Registers a console decision in registration order; Skip ends the chain.</summary>
    /// <param name="interceptors">The mod-owned interceptor service.</param>
    /// <param name="handler">Synchronous callback, valid only during the intercepted call.</param>
    /// <returns>A registration disposed automatically on startup failure or shutdown.</returns>
    public static IDisposable OnConsoleCommandExecuting(this InterceptorService interceptors,
        Action<ConsoleCommandInterception> handler)
    {
        ArgumentNullException.ThrowIfNull(interceptors);
        ArgumentNullException.ThrowIfNull(handler);
        return interceptors.Register<Invocation, Decision>("console.command.executing", input =>
        {
            var context = new ConsoleCommandInterception(input.OriginalCommand, input.Command, input.SelectedReferenceFormId);
            try
            {
                handler(context);
                return new Decision(context.Command, context.Skip);
            }
            finally
            {
                context.Complete();
            }
        });
    }

    private sealed record Invocation(string OriginalCommand, string Command, uint SelectedReferenceFormId);
    private sealed record Decision(string Command, bool Skip);
}
