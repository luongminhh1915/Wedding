using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class SwaggerDocumentTests
{
    [Fact]
    public async Task AllCurrentApiControllersGenerateSwaggerWithBinaryMultipartUploadFields()
    {
        // Discover the real API assembly without invoking Program, its database
        // initialization, hosted workers, HTTP listeners or any external service.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(UploadController).Assembly.GetName().Name,
            EnvironmentName = "SwaggerDocumentTests",
            ContentRootPath = AppContext.BaseDirectory,
            Args = []
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(UploadController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Synthetic full-controller Swagger regression",
            Version = "v1"
        }));
        await using var app = builder.Build();
        app.MapControllers();

        // Generate the complete document, not a filtered Upload-only document:
        // schema collisions and unsupported action metadata must also fail here.
        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.NotEmpty(document.Paths);
        var invitation = MultipartSchema(document, "/api/Upload/invitation-images");
        Assert.True(invitation.Properties.TryGetValue("file", out var singleFile));
        AssertBinary(document, singleFile!);

        var listing = MultipartSchema(document, "/api/Upload/images");
        Assert.True(listing.Properties.TryGetValue("files", out var files));
        var array = Resolve(document, files!);
        Assert.Equal("array", array.Type); Assert.NotNull(array.Items);
        AssertBinary(document, array.Items);

        Assert.True(FindPath(document, "/api/Listings/my-listings/page").Operations.ContainsKey(OperationType.Get));
        Assert.True(FindPath(document, "/api/auth/login").Operations.ContainsKey(OperationType.Post));
        Assert.True(FindPath(document, "/api/Contracts").Operations.ContainsKey(OperationType.Post));
        Assert.True(FindPath(document, "/api/Commissions/advance-requests").Operations.ContainsKey(OperationType.Get));
    }

    [Theory]
    [InlineData(nameof(UploadController.UploadInvitationImage), "Customer", "file", 9L * 1024 * 1024)]
    [InlineData(nameof(UploadController.UploadImages), "VendorOwner", "files", 81L * 1024 * 1024)]
    public void UploadOpenApiBindingRetainsRoleFormFieldAndRequestLimits(string actionName, string role, string formField, long limit)
    {
        Assert.NotNull(typeof(UploadController).GetCustomAttribute<AuthorizeAttribute>());
        var method = typeof(UploadController).GetMethod(actionName)!;
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Equal(role, Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Contains("multipart/form-data", Assert.Single(method.GetCustomAttributes<ConsumesAttribute>()).ContentTypes);
        var requestLimit = Assert.Single(method.CustomAttributes, attribute => attribute.AttributeType == typeof(RequestSizeLimitAttribute));
        Assert.Equal(limit, Assert.IsType<long>(Assert.Single(requestLimit.ConstructorArguments).Value));
        Assert.Equal(limit, method.GetCustomAttribute<RequestFormLimitsAttribute>()?.MultipartBodyLengthLimit);
        var fileParameter = method.GetParameters().Single(parameter => parameter.Name == formField);
        Assert.Null(fileParameter.GetCustomAttribute<FromFormAttribute>());
        Assert.Equal(formField == "file" ? typeof(IFormFile) : typeof(List<IFormFile>), fileParameter.ParameterType);
    }

    private static OpenApiPathItem FindPath(OpenApiDocument document, string path)
        => Assert.Single(document.Paths, item => string.Equals(item.Key, path, StringComparison.OrdinalIgnoreCase)).Value;

    private static OpenApiSchema MultipartSchema(OpenApiDocument document, string path)
    {
        var operation = FindPath(document, path).Operations[OperationType.Post];
        Assert.NotNull(operation.RequestBody);
        Assert.True(operation.RequestBody.Content.TryGetValue("multipart/form-data", out var body));
        Assert.NotNull(body?.Schema);
        var schema = Resolve(document, Assert.IsType<OpenApiSchema>(body?.Schema));
        Assert.Equal("object", schema.Type);
        return schema;
    }

    private static void AssertBinary(OpenApiDocument document, OpenApiSchema schema)
    {
        var resolved = Resolve(document, schema);
        Assert.Equal("string", resolved.Type);
        Assert.Equal("binary", resolved.Format);
    }

    private static OpenApiSchema Resolve(OpenApiDocument document, OpenApiSchema schema)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (schema.Reference != null)
        {
            var id = schema.Reference.Id;
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.True(seen.Add(id), $"Unexpected schema-reference cycle: {id}");
            Assert.True(document.Components.Schemas.TryGetValue(id, out var referenced), $"Missing OpenAPI schema: {id}");
            schema = Assert.IsType<OpenApiSchema>(referenced);
        }
        return schema;
    }
}
