using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MasteringModernCSharp.Ch11.Tests;

public class P100_AspireTests
{
    // Builds the app model without starting any containers, and
    // checks the wiring the AppHost describes.
    [Fact]
    public async Task App_model_wires_catalog_to_its_dependencies()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Ch11_AppHost>(ct);
        await using var app = await builder.BuildAsync(ct);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var names = model.Resources.Select(r => r.Name).ToList();
        Assert.Contains("cache", names);
        Assert.Contains("pricing", names);
        Assert.Contains("catalog", names);

        var catalog = model.Resources.OfType<ProjectResource>()
            .Single(r => r.Name == "catalog");
        Assert.True(catalog.TryGetAnnotationsOfType<WaitAnnotation>(out var waits));
        Assert.Contains(waits, w => w.Resource.Name == "cache");
        Assert.True(catalog.TryGetLastAnnotation<ReplicaAnnotation>(out var replicas));
        Assert.Equal(2, replicas.Replicas);
    }
}
