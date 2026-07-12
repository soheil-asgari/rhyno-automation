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

    [Fact]
    public void VerifyAllViewLinksPointToValidControllers()
    {
        // 1. Get all controllers in the application assembly
        var assembly = typeof(Program).Assembly;
        var controllerTypes = assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        // 2. Walk through Views directory to find all cshtml files
        var solutionDir = AppDomain.CurrentDomain.BaseDirectory;
        // Traverse up to find the project root
        var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(solutionDir, "..", "..", "..", ".."));
        if (!System.IO.Directory.Exists(System.IO.Path.Combine(projectRoot, "Views")))
        {
            // Try another level or fallback
            projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(solutionDir, "..", "..", ".."));
        }
        var viewsDir = System.IO.Path.Combine(projectRoot, "Views");
        if (!System.IO.Directory.Exists(viewsDir))
        {
             return;
        }

        var cshtmlFiles = System.IO.Directory.GetFiles(viewsDir, "*.cshtml", System.IO.SearchOption.AllDirectories);
        var missingControllers = new List<string>();

        var regex = new System.Text.RegularExpressions.Regex("asp-controller=\"([^\"]+)\"");

        foreach (var file in cshtmlFiles)
        {
            var content = System.IO.File.ReadAllText(file);
            var matches = regex.Matches(content);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var controllerName = match.Groups[1].Value;
                if (controllerName.Contains("@") || controllerName.Contains("{") || controllerName.StartsWith("identity", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetControllerClassName = $"{controllerName}Controller";
                if (!controllerTypes.Any(c => string.Equals(c, targetControllerClassName, StringComparison.OrdinalIgnoreCase)))
                {
                    missingControllers.Add($"In file: {System.IO.Path.GetFileName(file)} -> asp-controller=\"{controllerName}\" is invalid (No class {targetControllerClassName} found)");
                }
            }
        }

        if (missingControllers.Any())
        {
            Assert.Fail($"Found broken/dead links to non-existent controllers:\n{string.Join("\n", missingControllers.Distinct())}");
        }
    }
}
