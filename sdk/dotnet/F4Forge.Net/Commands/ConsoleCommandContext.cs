namespace F4Forge.Net.Commands;

/// <summary>One synchronous invocation in the game console's calling thread.</summary>
public sealed class ConsoleCommandContext
{
    private readonly List<string> _lines = [];
    private bool _completed;

    internal ConsoleCommandContext(ConsoleInvocation invocation)
    {
        Alias = invocation.Alias;
        Arguments = Array.AsReadOnly(invocation.Arguments);
        RawCommand = invocation.RawCommand;
        RawArguments = invocation.RawArguments;
        SelectedReferenceFormId = invocation.SelectedReferenceFormId;
    }

    /// <summary>The registered alias in lowercase.</summary>
    public string Alias { get; }

    /// <summary>Arguments with double quotes and quote escapes resolved.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>The original input, including the alias and whitespace.</summary>
    public string RawCommand { get; }

    /// <summary>The unparsed text following the alias and its separating whitespace.</summary>
    public string RawArguments { get; }

    /// <summary>Selected console reference, or zero if none is selected.</summary>
    public uint SelectedReferenceFormId { get; }

    /// <summary>Buffers a line for display when the handler returns. Valid only during this invocation.</summary>
    public void WriteLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_completed)
        {
            throw new InvalidOperationException("The console invocation has completed.");
        }
        _lines.Add(text);
    }

    internal ConsoleResult GetResult()
    {
        return new ConsoleResult(_lines.ToArray());
    }

    internal void Complete()
    {
        _completed = true;
    }
}
