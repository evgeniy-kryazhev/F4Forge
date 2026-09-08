namespace F4Forge.DotNet.Sdk;

public abstract class F4ForgePlugin
{
    public virtual string Id => GetType().FullName ?? GetType().Name;

    public virtual void OnLoad()
    {
    }

    public virtual void OnUnload()
    {
    }
}
