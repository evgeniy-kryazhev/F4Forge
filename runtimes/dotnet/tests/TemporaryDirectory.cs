namespace F4Forge.DotNet.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory(string category)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "f4forge-tests", category, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
