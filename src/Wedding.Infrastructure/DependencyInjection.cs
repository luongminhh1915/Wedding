using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wedding.Application.Common.Interfaces;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=WeddingPlatformDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, b => 
                b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => 
            provider.GetRequiredService<ApplicationDbContext>());

        // Đăng ký dịch vụ mật khẩu, JWT và VietQR
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddSingleton<IVietQrService, VietQrService>();

        // Đăng ký CurrentUserService — đọc UserId từ JWT claims
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Đăng ký Background Worker kiểm tra SLA Lead định kỳ (BR-003)
        services.AddHostedService<LeadSlaBackgroundService>();

        // Đăng ký Background Worker quét HĐ quá hạn 72h (BR-005)
        services.AddHostedService<ContractVerificationBackgroundService>();

        // Đăng ký Background Worker đối soát hoa hồng và xử lý trễ hạn (BR-007)
        services.AddHostedService<MonthlySettlementBackgroundService>();

        return services;
    }
}
