using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

var phase = "Init";
try
{
    phase = "ReadConfig";
    var configText = await File.ReadAllTextAsync(".deployment/private/home-cms-release-20261029/wedding.production.after.json");
    using var configDoc = JsonDocument.Parse(configText);
    var connStr = configDoc.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
    if (string.IsNullOrWhiteSpace(connStr)) throw new InvalidOperationException("Missing connection string.");

    var builder = new SqlConnectionStringBuilder(connStr);
    if (builder.DataSource != "sql1004.site4now.net" || builder.InitialCatalog != "db_ad0009_weddingmatch")
    {
        throw new InvalidOperationException($"Unexpected target: {builder.DataSource}/{builder.InitialCatalog}");
    }
    builder.ConnectTimeout = 30;

    phase = "PrepareVendorList";
    var rawNames = new[]
    {
        "CTM Palace",
        "Trống Đồng Palace",
        "Vạn Hoa Premium",
        "White Palace",
        "Hệ thống THE ADORA",
        "Lek Chi Bridal",
        "Nicole Bridal",
        "Chung Thanh Phong Bridal",
        "Cee's Bridal",
        "Adam Store",
        "Tina Lê Make Up",
        "Hạnh Lâm Makeup & Academy",
        "John Kim Wedding",
        "Mai Phan Makeup Artist",
        "Tee Le Studio",
        "Liti Florist",
        "Hands & Heart Floral Design",
        "Métaphore Floral",
        "Lá Floral",
        "Padma de Fleur",
        "The Planners",
        "Lutece Wedding",
        "MHs Planner",
        "Cleo Wedding",
        "Phi Điệp Wedding",
        "7799 Wedding Storyteller",
        "Lại Hằng Wedding",
        "Nắng Wedding",
        "Cưới Hỏi Quang Dũng",
        "Peonies Wedding",
        "Traqué Wedding Paper",
        "2TPRINT",
        "Diamond",
        "The Simple",
        "Nupakachi Wedding",
        "Cộng Studio",
        "Thien Tong Photography",
        "Mango Studio",
        "TuArts Wedding",
        "MC Hạnh Phúc",
        "MC Quang Bảo",
        "xFat Sound",
        "xLaam Band",
        "Harmony Band",
        "Đức Vinh Trans",
        "Xe Cưới Đông A",
        "Đông A",
        "Vân Hải",
        "Hoa Mai Tour",
        "Vietravel",
        "Saigontourist",
        "BestPrice Travel",
        "Lửa Việt Tours",
        "PYS Travel"
    };

    const string defaultPassword = "1234567890";
    phase = "HashPassword";
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

    phase = "OpenConnection";
    await using var connection = new SqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    phase = "ReadExistingMetadata";
    var existingLoginNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var existingSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT LoginName FROM dbo.Users WHERE LoginName IS NOT NULL;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingLoginNames.Add(reader.GetString(0));
        }
    }

    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT Slug FROM dbo.Vendors;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingSlugs.Add(reader.GetString(0));
        }
    }

    Guid? adminId = null;
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT TOP 1 Id FROM dbo.Users WHERE Role = 'SuperAdmin' AND IsActive = 1 ORDER BY CreatedAt ASC;";
        var val = await cmd.ExecuteScalarAsync();
        if (val is Guid g) adminId = g;
    }

    static string GenerateSlug(string phrase)
    {
        var normalized = phrase.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "doi-tac";
        return slug[..Math.Min(slug.Length, 68)].TrimEnd('-');
    }

    var importRecords = new List<VendorImportRecord>();
    for (int i = 0; i < rawNames.Length; i++)
    {
        var name = rawNames[i].Trim();
        var baseSlug = GenerateSlug(name);

        var loginName = baseSlug;
        var suffix = 1;
        while (existingLoginNames.Contains(loginName))
        {
            loginName = $"{baseSlug}-{suffix++}";
        }
        existingLoginNames.Add(loginName);

        var slug = baseSlug;
        suffix = 1;
        while (existingSlugs.Contains(slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        existingSlugs.Add(slug);

        importRecords.Add(new VendorImportRecord(
            Stt: i + 1,
            FullName: name,
            BrandName: name,
            LoginName: loginName,
            Slug: slug,
            Password: defaultPassword,
            UserId: Guid.NewGuid(),
            VendorId: Guid.NewGuid()
        ));
    }

    phase = "ExecuteInsertTransaction";
    await using (var tx = (SqlTransaction)await connection.BeginTransactionAsync())
    {
        var now = DateTime.UtcNow;

        foreach (var rec in importRecords)
        {
            // Insert User
            await using (var userCmd = connection.CreateCommand())
            {
                userCmd.Transaction = tx;
                userCmd.CommandText = @"
                    INSERT INTO dbo.Users (
                        Id, FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt, LoginName, SecurityVersion
                    ) VALUES (
                        @Id, @FullName, NULL, @PhoneNumber, @PasswordHash, 'VendorOwner', 1, @CreatedAt, @LoginName, 0
                    );";
                userCmd.Parameters.AddWithValue("@Id", rec.UserId);
                userCmd.Parameters.AddWithValue("@FullName", rec.FullName);
                userCmd.Parameters.AddWithValue("@PhoneNumber", "");
                userCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                userCmd.Parameters.AddWithValue("@CreatedAt", now);
                userCmd.Parameters.AddWithValue("@LoginName", rec.LoginName);
                await userCmd.ExecuteNonQueryAsync();
            }

            // Insert Vendor
            await using (var vendorCmd = connection.CreateCommand())
            {
                vendorCmd.Transaction = tx;
                vendorCmd.CommandText = @"
                    INSERT INTO dbo.Vendors (
                        Id, UserId, BrandName, Slug, ContactPerson, Hotline, Address, City, OperatingRegions,
                        CommissionRate, RatingAvg, TotalReviews, IsTopRated, IsActive, IsVerified, AllowInCombos,
                        MaxComboDiscount, CreatedAt
                    ) VALUES (
                        @Id, @UserId, @BrandName, @Slug, @ContactPerson, @Hotline, NULL, @City, @OperatingRegions,
                        @CommissionRate, @RatingAvg, @TotalReviews, 0, 1, 0, 1,
                        0.0, @CreatedAt
                    );";
                vendorCmd.Parameters.AddWithValue("@Id", rec.VendorId);
                vendorCmd.Parameters.AddWithValue("@UserId", rec.UserId);
                vendorCmd.Parameters.AddWithValue("@BrandName", rec.BrandName);
                vendorCmd.Parameters.AddWithValue("@Slug", rec.Slug);
                vendorCmd.Parameters.AddWithValue("@ContactPerson", rec.FullName);
                vendorCmd.Parameters.AddWithValue("@Hotline", "");
                vendorCmd.Parameters.AddWithValue("@City", "");
                vendorCmd.Parameters.AddWithValue("@OperatingRegions", "[\"*\"]");
                vendorCmd.Parameters.AddWithValue("@CommissionRate", 0.0m);
                vendorCmd.Parameters.AddWithValue("@RatingAvg", 0.0m);
                vendorCmd.Parameters.AddWithValue("@TotalReviews", 0);
                vendorCmd.Parameters.AddWithValue("@CreatedAt", now);
                await vendorCmd.ExecuteNonQueryAsync();
            }

            // Insert AuditLog
            await using (var auditCmd = connection.CreateCommand())
            {
                auditCmd.Transaction = tx;
                auditCmd.CommandText = @"
                    INSERT INTO dbo.AuditLogs (
                        Id, ActorId, ActorRole, Action, EntityType, EntityId, DataJson, CreatedAt
                    ) VALUES (
                        @Id, @ActorId, 'SuperAdmin', 'User.Create', 'User', @EntityId, @DataJson, @CreatedAt
                    );";
                auditCmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                auditCmd.Parameters.AddWithValue("@ActorId", (object?)adminId ?? DBNull.Value);
                auditCmd.Parameters.AddWithValue("@EntityId", rec.UserId.ToString());
                auditCmd.Parameters.AddWithValue("@DataJson", JsonSerializer.Serialize(new { Role = "VendorOwner", VendorId = rec.VendorId }));
                auditCmd.Parameters.AddWithValue("@CreatedAt", now);
                await auditCmd.ExecuteNonQueryAsync();
            }
        }

        await tx.CommitAsync();
    }

    phase = "VerifyPostCounts";
    int userCount, vendorCount;
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.Users;";
        userCount = (int)await cmd.ExecuteScalarAsync();
    }
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.Vendors;";
        vendorCount = (int)await cmd.ExecuteScalarAsync();
    }

    var result = new
    {
        status = "Success",
        importedCount = importRecords.Count,
        totalUsersInDb = userCount,
        totalVendorsInDb = vendorCount,
        accounts = importRecords.Select(r => new
        {
            stt = r.Stt,
            brandName = r.BrandName,
            loginName = r.LoginName,
            password = r.Password
        }).ToList()
    };

    var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync("artifacts/vendor-studio-deploy-20261030/vendors-import-result.json", resultJson);
    Console.WriteLine(resultJson);
}
catch (Exception ex)
{
    var err = new
    {
        status = "Failed",
        phase,
        exceptionType = ex.GetType().Name,
        message = ex.Message
    };
    Console.Error.WriteLine(JsonSerializer.Serialize(err, new JsonSerializerOptions { WriteIndented = true }));
    Environment.ExitCode = 1;
}

record VendorImportRecord(
    int Stt,
    string FullName,
    string BrandName,
    string LoginName,
    string Slug,
    string Password,
    Guid UserId,
    Guid VendorId
);

