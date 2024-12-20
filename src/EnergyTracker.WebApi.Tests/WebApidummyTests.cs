using FluentAssertions;

namespace EnergyTracker.WebApi.Tests;

public class WebApidummyTests
{
    [Fact]
    public void True_ShouldBeTrue()
    {
        var theBool = true;
        theBool.Should().BeTrue();
    }
}
