using Reticle.Core.Model;

namespace Reticle.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssembly_Loads()
    {
        Assert.NotNull(typeof(Rgba).Assembly);
    }
}
