using F4Forge.Net.Callbacks;
using F4Forge.Net.Messaging;

namespace F4Forge.Net.Console;

internal sealed class ConsoleService(IMessageChannel messages) : IConsole
{
    public IDisposable AddHandler(string alias, Action<ConsoleCommandContext> handler)
    {
        ArgumentNullException.ThrowIfNull(alias);
        ArgumentNullException.ThrowIfNull(handler);
        ValidateAlias(alias);
        var normalizedAlias = alias.ToLowerInvariant();
        if (!messages.Call<bool>("console.validate_alias", new { Alias = normalizedAlias }))
        {
            throw new InvalidOperationException($"Command alias is unavailable: {alias}");
        }
        // Register through the mod's scoped channel so unload and startup rollback
        // use the same callback barrier as all other message handlers.
        return messages.Register<ConsoleInvocation, ConsoleResult>("console.command." + normalizedAlias, invocation =>
        {
            if (!string.IsNullOrEmpty(invocation.ParseError))
            {
                throw new FormatException(invocation.ParseError);
            }
            return InvocationContext.Invoke(new ConsoleCommandContext(invocation), handler,
                static context => context.GetResult());
        });
    }

    private static void ValidateAlias(string alias)
    {
        if (alias.Length is < 1 or > 64)
        {
            throw new ArgumentException("Command alias must contain 1 to 64 ASCII characters.", nameof(alias));
        }
        for (var index = 0; index < alias.Length; index++)
        {
            var character = alias[index];
            var validStart = char.IsAsciiLetter(character) || character == '_';
            var validContinuation = validStart || char.IsAsciiDigit(character) || character is '.' or '-';
            if (!(index == 0 ? validStart : validContinuation))
            {
                throw new ArgumentException("Command alias must match [A-Za-z_][A-Za-z0-9_.-]*.", nameof(alias));
            }
        }
    }
}

internal sealed record ConsoleInvocation(
    string Alias,
    string[] Arguments,
    string RawCommand,
    string RawArguments,
    uint SelectedReferenceFormId,
    string ParseError);

internal sealed record ConsoleResult(string[] Lines);
