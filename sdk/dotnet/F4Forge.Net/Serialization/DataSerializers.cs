namespace F4Forge.Net.Serialization;

internal static class DataSerializers
{
    public static IDataSerializer Create(string format)
    {
        IDataSerializer? selected = null;
        // SDK codec implementations are discovered from new source files, without
        // adding cases to a central switch. Mod assemblies do not control the format.
        foreach (var type in typeof(IDataSerializer).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !typeof(IDataSerializer).IsAssignableFrom(type))
            {
                continue;
            }
            var serializer = (IDataSerializer)Activator.CreateInstance(type)!;
            if (serializer.Format != format)
            {
                continue;
            }
            if (selected is not null)
            {
                throw new InvalidOperationException($"Duplicate serializer for '{format}'.");
            }
            selected = serializer;
        }
        return selected ?? throw new NotSupportedException($"Unsupported data format '{format}'.");
    }
}
