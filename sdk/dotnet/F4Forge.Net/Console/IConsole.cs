namespace F4Forge.Net.Console;

/// <summary>Custom aliases entered directly into the Fallout 4 console.</summary>
public interface IConsole
{
    /// <summary>
    /// Registers a synchronous handler. Aliases are case insensitive and cannot replace
    /// built-in commands. Dispose the registration to remove it early; mod shutdown removes it automatically.
    /// </summary>
    IDisposable AddHandler(string alias, Action<ConsoleCommandContext> handler);
}
