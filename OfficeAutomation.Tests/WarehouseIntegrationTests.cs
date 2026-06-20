using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OfficeAutomation.Tests;

public class WarehouseIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WarehouseIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TestProjectIsWired()
    {
        Assert.NotNull(_factory);
    }
}
