using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Console;

/// <summary>Runs engine console commands through the existing game-thread scheduler.</summary>
public static class ConsoleExecutionExtensions
{
    /// <summary>Executes a command using the engine console, preserving other plugins' hooks.</summary>
    /// <param name="context">Owning mod context.</param>
    /// <param name="command">Console command text.</param>
    /// <param name="cancellationToken">Cancels pending execution.</param>
    /// <returns>Completes after submission; engine errors remain in the console log.</returns>
    public static Task ExecuteConsoleCommandAsync(this ModContext context, string command,
        CancellationToken cancellationToken = default) =>
        context.GetSingleton<GameThread>().RunAsync(() =>
        {
            context.Messages.Call<bool>("console.execute", new { Command = command });
        }, cancellationToken);
}
