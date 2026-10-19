using F4Forge.Net.Messaging;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Logging.Commands;

/// <summary>Writes a message through the native logger.</summary>
/// <param name="Level">Native log level, such as info, warning or error.</param>
/// <param name="Source">Source prefix identifying the mod.</param>
/// <param name="Message">The message, including any Unicode text.</param>
public sealed record WriteLog(string Level, string Source, string Message) : ICommand<bool>
{
    /// <inheritdoc />
    public string MethodName => "logging.write";

    /// <inheritdoc />
    public object Arguments => new { Level, Source, Message };

    /// <inheritdoc />
    public void Validate(IMessageChannel messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Level);
        ArgumentNullException.ThrowIfNull(Source);
        ArgumentNullException.ThrowIfNull(Message);
    }
}
