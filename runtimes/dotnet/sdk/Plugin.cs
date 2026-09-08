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

    public virtual void OnUnload()
    {
    }
}
