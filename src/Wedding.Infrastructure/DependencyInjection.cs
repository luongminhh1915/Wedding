using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wedding.Application.Common.Interfaces;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var databaseProvider = configuration["Database:Provider"]
            ?? configuration["DatabaseProvider"]
            ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=WeddingPlatformDb.db";

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sqliteConnection = new SqliteConnectionStringBuilder(connectionString);

                // Resolve relative paths against the application content root, never the current
                // process directory. This keeps local restarts and deployed instances predictable.
                if (!string.Equals(sqliteConnection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
                {
                    sqliteConnection.DataSource = Path.IsPathRooted(sqliteConnection.DataSource)
                        ? sqliteConnection.DataSource
                        : Path.GetFullPath(sqliteConnection.DataSource, hostEnvironment.ContentRootPath);
                    var databaseDirectory = Path.GetDirectoryName(sqliteConnection.DataSource);
                    if (!string.IsNullOrWhiteSpace(databaseDirectory))
                    {
                        Directory.CreateDirectory(databaseDirectory);
                    }
                }

                options.UseSqlite(sqliteConnection.ConnectionString);
            }
            else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, b =>
                    b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Database provider '{databaseProvider}' is not supported. Use Sqlite or SqlServer.");
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => 
            provider.GetRequiredService<ApplicationDbContext>());

        // Đăng ký dịch vụ mật khẩu, JWT và VietQR
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddSingleton<IVietQrService, VietQrService>();
        services.AddScoped<IAdvancePaymentService, AdvancePaymentService>(); // P0: lưu DB thay vì bộ nhớ
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<INotificationSender, DurableNotificationSender>();
        services.AddSingleton<SmtpDelivery>();
        services.AddSingleton<IOtpDelivery>(provider => provider.GetRequiredService<SmtpDelivery>());
        services.AddHttpClient("identity", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
        services.AddSingleton<IMockPaymentGateway, MockPaymentGateway>();
        services.AddScoped<IBillingService, PaymentLedgerService>();

        // Đăng ký CurrentUserService — đọc UserId từ JWT claims
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Đăng ký Background Worker kiểm tra SLA Lead định kỳ (BR-003)
        services.AddHostedService<LeadSlaBackgroundService>();

        // Đăng ký Background Worker quét HĐ quá hạn 72h (BR-005)
        services.AddHostedService<ContractVerificationBackgroundService>();

        // Đăng ký Background Worker đối soát hoa hồng và xử lý trễ hạn (BR-007)
        services.AddHostedService<MonthlySettlementBackgroundService>();

        // P3: tự hết hạn lịch hẹn chờ cọc/chờ duyệt quá giờ
        services.AddHostedService<AppointmentHousekeepingBackgroundService>();
        services.AddHostedService<NotificationBackgroundService>();
        services.AddHostedService<ReleasePeriod2BackgroundService>();

        return services;
    }
}
