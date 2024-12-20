using FluentAssertions;

namespace EnergyTracker.BlazorClient.Tests;

public class BlazorDummyTests
{
    [Fact]
    public void True_EvaluesToTrue()
    {
        var theBool = true;
        theBool.Should().BeTrue();
    }
}
