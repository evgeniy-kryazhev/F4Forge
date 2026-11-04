using System.Text.Json;
using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Papyrus;

/// <summary>Registers revocable, synchronous native Papyrus functions for one mod.</summary>
public sealed class GamePapyrusFunctions : IGameSingleton<GamePapyrusFunctions>
{
    private readonly ModContext _context;

    private GamePapyrusFunctions(ModContext context)
    {
        _context = context;
    }

    static GamePapyrusFunctions IGameSingleton<GamePapyrusFunctions>.Create(ModContext context) => new(context);

    /// <summary>Registers a static function. Its compiled script declaration must be installed separately.</summary>
    /// <param name="script">Papyrus script name.</param>
    /// <param name="name">Native function name.</param>
    /// <param name="returnType">none, bool, int, float, or string.</param>
    /// <param name="parameters">Ordered primitive parameter types.</param>
    /// <param name="handler">Synchronous handler; never wait on asynchronous work inside this callback.</param>
    /// <param name="cancellationToken">Cancels work before it starts.</param>
    /// <returns>A registration owned by the mod lifetime.</returns>
    public async Task<IDisposable> RegisterAsync(string script, string name, string returnType,
        string[] parameters, Func<JsonElement[], object?> handler, CancellationToken cancellationToken = default)
    {
        using var operation = _context.Lifetime.Enter();
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(handler);
        var signature = parameters.ToArray();
        var callback = "papyrus.managed." + Guid.NewGuid().ToString("N");
        var registration = _context.Messages.Register<JsonElement[], object?>(callback, handler);
        try
        {
            var key = await _context.GetSingleton<GameThread>().RunAsync(() =>
                _context.Messages.Call<string>("papyrus.functions.register", new
                {
                    Script = script, Name = name, ReturnType = returnType, Parameters = signature, Callback = callback
                }), cancellationToken).ConfigureAwait(false);
            return _context.Lifetime.Own(new Registration(_context, key, registration));
        }
        catch
        {
            registration.Dispose();
            throw;
        }
    }

    private sealed class Registration(ModContext context, string key, IDisposable callback) : IDisposable
    {
        private int _closed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }
            try
            {
                context.CleanupMessages.Call<bool>("papyrus.functions.release", new { Key = key });
            }
            finally
            {
                callback.Dispose();
            }
        }
    }
}
