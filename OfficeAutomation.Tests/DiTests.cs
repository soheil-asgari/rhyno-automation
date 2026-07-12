using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OfficeAutomation.Tests;

public class DiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void NoDuplicateKeyServicesAreRegistered()
    {
        IServiceCollection? registeredServices = null;

        // Force design-time mode to prevent background tasks starting
        Environment.SetEnvironmentVariable("EF_DESIGN_TIME", "true");

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                registeredServices = services;
            });
        }).CreateClient();

        Assert.NotNull(registeredServices);

        // Exclude framework services (Microsoft/System) and generic/open-generic registrations that are expected
        var duplicates = registeredServices
            .GroupBy(d => d.ServiceType)
            .Where(g => g.Count() > 1 
                        && g.Key.FullName != null 
                        && !g.Key.FullName.StartsWith("Microsoft.") 
                        && !g.Key.FullName.StartsWith("System.")
                        && !g.Key.IsGenericType)
            .Select(g => new
            {
                ServiceType = g.Key,
                Registrations = g.Select(r => r.ImplementationType?.Name ?? r.ImplementationInstance?.GetType().Name ?? "Factory").ToList()
            })
            .ToList();

        if (duplicates.Any())
        {
            var details = string.Join("\n", duplicates.Select(d => $"{d.ServiceType.FullName}: registered {d.Registrations.Count} times ({string.Join(", ", d.Registrations)})"));
            Assert.Fail($"Duplicate DI registrations found:\n{details}");
        }
    }
}
