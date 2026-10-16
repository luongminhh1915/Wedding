using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Wedding.Application;
using Wedding.Infrastructure;
using Wedding.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký các tầng Clean Architecture
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// 2. Cấu hình JWT Authentication
var secretKey = builder.Configuration["JwtSettings:Secret"] 
    ?? "WeddingPlatformSuperSecretKey2026CleanArchitectureJWTSecretLongEnough123456!";
if (!builder.Environment.IsDevelopment() && (secretKey.Length < 32 || secretKey.StartsWith("WeddingPlatformSuperSecret", StringComparison.Ordinal)))
    throw new InvalidOperationException("Configure a unique JwtSettings__Secret (32+ characters) before deploying.");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "WeddingPlatform",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "WeddingPlatformClient",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Hỗ trợ truyền JWT Token qua Query string cho SignalR WebSocket
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var role = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!Guid.TryParse(id, out var userId)) { context.Fail("Invalid user"); return; }
                var db = context.HttpContext.RequestServices.GetRequiredService<Wedding.Application.Common.Interfaces.IApplicationDbContext>();
                var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.IsActive).Select(u => new { u.Role }).FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                if (user == null || user.Role.ToString() != role) context.Fail("Account inactive or role changed");
            }
        };
    });

builder.Services.AddAuthorization();

// 3. Web API Controllers, SignalR & JSON
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 4. Cấu hình Swagger hỗ trợ JWT Bearer Auth
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Wedding Service Platform API",
        Version = "v1",
        Description = "Hệ thống Nền tảng Dịch vụ Cưới (Lean MVP Clean Architecture)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập token JWT theo định dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 5. Cấu hình CORS cho React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173", "http://localhost:3000" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wedding Service Platform API v1");
    });
}

// Global Exception Handler Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Wedding.Domain.Exceptions.DomainException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { success = false, message = ex.Message });
    }
    catch (Wedding.Domain.Exceptions.NotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { success = false, message = ex.Message });
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        app.Logger.LogError(ex, "Unhandled request error {TraceId}", context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(new { success = false, message = "Đã xảy ra lỗi máy chủ nội bộ.", traceId = context.TraceIdentifier });
    }
});

app.UseCors("AllowClient");
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Khởi tạo schema. Dữ liệu mẫu chỉ được seed khi Database:SeedDemoData=true
// (mặc định chỉ trong appsettings.Development.json) và database hoàn toàn trống.
await Wedding.Infrastructure.Persistence.DbInitializer.SeedAsync(app.Services);

app.Run();
