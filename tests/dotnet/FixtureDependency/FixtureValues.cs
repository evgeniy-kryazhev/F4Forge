namespace F4Forge.Tests.FixtureDependency;

public static class FixtureValues
{
    public static T Identity<T>(T value)
    {
        return value;
    }
}
