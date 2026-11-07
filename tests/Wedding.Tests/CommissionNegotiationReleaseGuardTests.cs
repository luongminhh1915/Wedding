using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public sealed class CommissionNegotiationReleaseGuardTests
{
    [Fact]
    public async Task FeatureFilterIsNotDiscoveredAsAnHttpActionOrSwaggerBodyContract()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(CommissionNegotiationsController).Assembly.GetName().Name,
            EnvironmentName = "CommissionNegotiationReleaseGuardTests",
            ContentRootPath = AppContext.BaseDirectory,
            Args = []
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(CommissionNegotiationsController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.ControllerTypeInfo.AsType() == typeof(CommissionNegotiationsController)).ToArray();
        Assert.Equal(9, actions.Length);
        Assert.DoesNotContain(actions, a => a.MethodInfo.Name == nameof(CommissionNegotiationsController.OnActionExecutionAsync));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public async Task FoundationIsDefaultOffBeforeActionOrMediatorCanQueryItsTables(string? configured, bool expectedAction)
    {
        var values = new Dictionary<string, string?>();
        if (configured != null) values[CommissionNegotiationsController.EnabledConfigurationKey] = configured;
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        // A null mediator deliberately makes any accidental dispatch observable.
        var controller = new CommissionNegotiationsController(null!, config);
        var context = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(context, filters, new Dictionary<string, object?>(), controller);
        var called = false;
        await controller.OnActionExecutionAsync(executing, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(context, filters, controller));
        });
        Assert.Equal(expectedAction, called);
        if (expectedAction) Assert.Null(executing.Result);
        else Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(executing.Result).StatusCode);
    }
}
