namespace F4Forge.Net.Mods;

internal sealed class ModLifetime
{
    private int _closed;

    public void Close()
    {
        Interlocked.Exchange(ref _closed, 1);
    }

    public void ThrowIfClosed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
    }
}
