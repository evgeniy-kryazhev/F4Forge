using System.Text;
using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Resources;

/// <summary>Reads small files through the engine resource system, including BA2 archives.</summary>
public sealed class GameResources : IGameSingleton<GameResources>
{
    private readonly ModContext _context;

    private GameResources(ModContext context)
    {
        _context = context;
    }

    static GameResources IGameSingleton<GameResources>.Create(ModContext context) => new(context);

    /// <summary>Reads a text resource up to 128 KiB, honoring UTF-8 and UTF-16 byte-order marks.</summary>
    /// <param name="path">Path relative to game resources, without a Data prefix.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>Decoded text, or null if no resource exists.</returns>
    public async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<int[]?>("resources.read", new { Path = path }), cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }
        using var stream = new MemoryStream(bytes.Select(value => checked((byte)value)).ToArray());
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
