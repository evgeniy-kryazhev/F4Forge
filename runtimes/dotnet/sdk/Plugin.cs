namespace F4Forge.DotNet.Sdk;

public abstract class F4ForgePlugin
{
    public virtual string Id => GetType().FullName ?? GetType().Name;

    public virtual void OnLoad()
    {
    }

    public virtual void OnLoad(F4ForgePluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        OnLoad();
    }

    /// <summary>
    /// Called during an explicit runtime unload or reload. Process termination is not a guaranteed callback boundary.
    /// </summary>
    public virtual void OnUnload()
    {
    }
}
