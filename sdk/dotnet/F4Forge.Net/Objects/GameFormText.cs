using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Objects;

/// <summary>Reads localized description text from game forms.</summary>
public sealed class GameFormText : IGameSingleton<GameFormText>
{
    private readonly ModContext _context;

    private GameFormText(ModContext context)
    {
        _context = context;
    }

    static GameFormText IGameSingleton<GameFormText>.Create(ModContext context) => new(context);

    /// <summary>Reads a form description using a plugin-local or runtime identifier.</summary>
    /// <param name="plugin">Plugin filename, or empty for a runtime form ID.</param>
    /// <param name="localId">Plugin-local form ID, or runtime ID if plugin is empty.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>Localized description, or empty if the form has no description.</returns>
    public Task<string> ReadDescriptionAsync(string plugin, uint localId, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<string>("forms.description", new { Plugin = plugin, LocalId = localId }), cancellationToken);
}
