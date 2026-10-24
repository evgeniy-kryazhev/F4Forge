using F4Forge.Net.Events;

namespace F4Forge.Net.Tests;

public sealed class EventNamingTests
{
    [Fact]
    public void AllEventContractsUseTheEventSuffix()
    {
        var assemblies = new[] { typeof(IGameEvent).Assembly, typeof(EventNamingTests).Assembly };
        var eventTypes = assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsInterface && typeof(IGameEvent).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(eventTypes);
        foreach (var eventType in eventTypes)
        {
            Assert.True(eventType.Name.EndsWith("Event", StringComparison.Ordinal),
                $"Event contract {eventType.FullName} must end with Event.");
        }
    }
}
