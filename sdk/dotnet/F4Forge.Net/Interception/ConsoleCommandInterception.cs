namespace F4Forge.Net.Interception;

/// <summary>A synchronous decision before console routing or engine execution.</summary>
public sealed class ConsoleCommandInterception
{
    private string _command;
    private bool _skip;
    private bool _completed;

    internal ConsoleCommandInterception(string originalCommand, string command, uint selectedReferenceFormId)
    {
        OriginalCommand = originalCommand;
        _command = command;
        SelectedReferenceFormId = selectedReferenceFormId;
    }

    /// <summary>The input before any interceptor changed it.</summary>
    public string OriginalCommand { get; }

    /// <summary>The command passed to the next interceptor and then to routing.</summary>
    public string Command
    {
        get => _command;
        set
        {
            ThrowIfCompleted();
            ArgumentNullException.ThrowIfNull(value);
            if (value.Contains('\0'))
            {
                throw new ArgumentException("A console command cannot contain NUL.", nameof(value));
            }
            _command = value;
        }
    }

    /// <summary>The selected console reference, or zero.</summary>
    public uint SelectedReferenceFormId { get; }

    /// <summary>Cancels routing and original engine execution, stopping the interceptor chain.</summary>
    public bool Skip
    {
        get => _skip;
        set
        {
            ThrowIfCompleted();
            _skip = value;
        }
    }

    internal void Complete() => _completed = true;

    private void ThrowIfCompleted()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The interceptor invocation has completed.");
        }
    }
}
