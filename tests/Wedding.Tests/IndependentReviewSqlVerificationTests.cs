using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class IndependentReviewSqlVerificationTests
{
    private const string PreviousMigration = "20261020055151_PrivateServiceAgreements";
    private const string ReviewMigration = "20261020123641_IndependentShopServiceComboReviews";
    private const string Content = "Synthetic legacy review content retained exactly during the upgrade.";

    [SqlServerFact]
    public async Task Phase4CUpgradePreservesLegacyDataReplaysConstraintsAndRefusesUnsafeDowngrades()
    {
        await using var owned = await OwnedDatabase.CreateAsync();
        var db = owned.Db;
        var migrator = db.GetService<IMigrator>();
        owned.AssertTarget();
        await migrator.MigrateAsync(PreviousMigration);
        await AssertLaterIdentityColumnAbsentAsync(db);
        var seed = await SeedLegacyAsync(db);
        var original = await CaptureOriginalTablesAsync(db);

        // Execute the real SQL Server idempotent upgrade script twice, rather than only
        // asking MigrateAsync to observe an already-recorded migration.
        var script = migrator.GenerateScript(PreviousMigration, ReviewMigration,
            MigrationsSqlGenerationOptions.Idempotent);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(batch => !string.IsNullOrWhiteSpace(batch)).ToArray();
        await db.Database.ExecuteSqlRawAsync("SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;");
        for (var replay = 0; replay < 2; replay++)
            foreach (var batch in batches) await db.Database.ExecuteSqlRawAsync(batch);
        await migrator.MigrateAsync(ReviewMigration);
        await migrator.MigrateAsync(ReviewMigration);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(ReviewMigration, (await db.Database.GetAppliedMigrationsAsync()).Last());
        await AssertLaterIdentityColumnAbsentAsync(db);
        await AssertOriginalTablesAsync(db, original);
        Assert.Equal(2, await db.Reviews.CountAsync());
        Assert.All(await db.Reviews.AsNoTracking().ToListAsync(), review => Assert.Equal(ReviewTargetType.Service, review.TargetType));
        var legacyOrder = await db.ComboOrders.AsNoTracking().SingleAsync(order => order.Id == seed.OrderId);
        Assert.Null(legacyOrder.ComboTemplateRevisionId);
        Assert.Null(legacyOrder.ComboTemplateSnapshotJson);
        // This boundary predates operating-area migration21; read only the historical rating columns.
        var vendor = await db.Vendors.AsNoTracking().Where(item => item.Id == seed.VendorId)
            .Select(item => new { item.RatingAvg, item.TotalReviews, item.IsTopRated }).SingleAsync();
        Assert.Equal(0m, vendor.RatingAvg); Assert.Equal(0, vendor.TotalReviews); Assert.False(vendor.IsTopRated);
        await AssertSchemaAsync(db);

        // A new transaction may review the same Service despite a legacy consultation.
        var service = Guid.NewGuid();
        await InsertReviewAsync(db, seed, service, "Service", seed.ContractId, null, null, seed.ListingId);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Service",
            seed.ContractId, null, null, seed.ListingId), 2601, 2627);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Service",
            null, null, null, seed.ListingId), 2601, 2627);
        await AssertDownRefusedAsync(owned, "repeated Service reviews");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [Reviews] WHERE [Id] = {service}");
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Service", null, null, null, null), 547);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Shop", seed.ContractId, seed.OrderId, null, null), 547);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Combo", null, null, seed.TemplateId, null), 547);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Unknown", null, null, null, seed.ListingId), 547);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Shop", null, Guid.NewGuid(), null, null), 547);

        // Independent Shop and Service targets can share a contract. Shop ComboOrder
        // uniqueness is separate from the individual component contract key.
        var shop = Guid.NewGuid(); var comboShop = Guid.NewGuid(); var combo = Guid.NewGuid();
        await InsertReviewAsync(db, seed, shop, "Shop", seed.ContractId, null, null, null);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Shop",
            seed.ContractId, null, null, null), 2601, 2627);
        await InsertReviewAsync(db, seed, comboShop, "Shop", null, seed.OrderId, null, null);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Shop",
            null, seed.OrderId, null, null), 2601, 2627);
        // This insertion tests the DB key/shape, not application eligibility of a legacy order.
        await InsertReviewAsync(db, seed, combo, "Combo", null, seed.OrderId, seed.TemplateId, null);
        await ExpectSqlFailureAsync(() => InsertReviewAsync(db, seed, Guid.NewGuid(), "Combo",
            null, seed.OrderId, seed.TemplateId, null), 2601, 2627);
        await AssertDownRefusedAsync(owned, "non-Service reviews");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [Reviews] WHERE [Id] IN ({shop}, {comboShop}, {combo})");

        var pendingEdit = Guid.NewGuid(); var rejectedEdit = Guid.NewGuid(); var approvedEdit = Guid.NewGuid();
        await InsertEditAsync(db, seed, pendingEdit, 1, "Pending");
        await ExpectSqlFailureAsync(() => InsertEditAsync(db, seed, Guid.NewGuid(), 2, "Pending"), 2601, 2627);
        await InsertEditAsync(db, seed, rejectedEdit, 2, "Rejected");
        await InsertEditAsync(db, seed, approvedEdit, 3, "Approved");
        await ExpectSqlFailureAsync(() => InsertEditAsync(db, seed, Guid.NewGuid(), 4, "Approved"), 2601, 2627);
        await ExpectSqlFailureAsync(() => InsertEditAsync(db, seed, Guid.NewGuid(), 2, "Rejected"), 2601, 2627);
        db.ChangeTracker.Clear();
        var edit = await db.ReviewEditRequests.SingleAsync(row => row.Id == pendingEdit);
        db.Entry(edit).Property(row => row.BeforeContent).CurrentValue = "Rewritten history";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        edit = await db.ReviewEditRequests.SingleAsync(row => row.Id == pendingEdit);
        db.ReviewEditRequests.Remove(edit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await AssertDownRefusedAsync(owned, "review edit history");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [ReviewEditRequests] WHERE [Id] IN ({pendingEdit}, {rejectedEdit}, {approvedEdit})");

        await ExpectSqlFailureAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [ComboOrders] SET [ComboTemplateRevisionId] = {Guid.NewGuid()} WHERE [Id] = {seed.OrderId}"), 547);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [ComboOrders] SET [ComboTemplateRevisionId] = {seed.RevisionId}, [ComboTemplateSnapshotJson] = {"{\"syntheticAuthoritativeRevision\":true}"} WHERE [Id] = {seed.OrderId}");
        var snapshottedOrder = await db.ComboOrders.SingleAsync(order => order.Id == seed.OrderId);
        db.Entry(snapshottedOrder).Property(order => order.ComboTemplateSnapshotJson).CurrentValue = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await AssertDownRefusedAsync(owned, "ComboOrder version snapshots");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [ComboOrders] SET [ComboTemplateRevisionId] = NULL, [ComboTemplateSnapshotJson] = NULL WHERE [Id] = {seed.OrderId}");

        // Only the fully safe baseline is downgraded, and only inside this owned test DB.
        owned.AssertTarget();
        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(PreviousMigration, (await db.Database.GetAppliedMigrationsAsync()).Last());
        await AssertOriginalTablesAsync(db, original);
        var restoredVendor = await db.Vendors.AsNoTracking().Where(item => item.Id == seed.VendorId)
            .Select(item => new { item.RatingAvg, item.TotalReviews }).SingleAsync();
        Assert.Equal(4.7m, restoredVendor.RatingAvg); Assert.Equal(2, restoredVendor.TotalReviews);
        owned.AssertTarget();
        await migrator.MigrateAsync(ReviewMigration);
        await AssertOriginalTablesAsync(db, original);
    }

    private static async Task<Seed> SeedLegacyAsync(ApplicationDbContext db)
    {
        var actors = new List<User>();
        User AddUser(UserRole role)
        {
            var user = User.Create("Synthetic Phase4C actor", $"{Guid.NewGuid():N}@review4c.example.invalid", "0900000000", "synthetic-no-login", role);
            actors.Add(user); return user;
        }
        var owner = AddUser(UserRole.VendorOwner); var customer = AddUser(UserRole.Customer);
        var otherCustomer = AddUser(UserRole.Customer); var moderator = AddUser(UserRole.Moderator);
        // This database intentionally predates the Facebook identity migration.
        // Seed only its historical columns, not the newer EF User mapping.
        foreach (var actor in actors)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [Users] ([Id], [FullName], [Email], [PhoneNumber], [PasswordHash], [Role], [IsActive], [CreatedAt])
                VALUES ({actor.Id}, {actor.FullName}, {actor.Email}, {actor.PhoneNumber}, {actor.PasswordHash}, {actor.Role.ToString()}, {actor.IsActive}, {actor.CreatedAt})
                """);
        var vendor = Vendor.Create(owner.Id, "Synthetic Phase4C vendor", $"review4c-{Guid.NewGuid():N}", "Synthetic owner", "0900000000", "HCM", .08m);
        vendor.UpdateRating(4.7m, 2);
        // Keep this fixture at the real Phase4C schema, without adding today's OperatingRegions column.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Vendors] ([Id],[UserId],[BrandName],[Slug],[ContactPerson],[Hotline],[City],
                [CommissionRate],[RatingAvg],[TotalReviews],[IsActive],[IsVerified],[IsTopRated],
                [AllowInCombos],[MaxComboDiscount],[CreatedAt])
            VALUES ({vendor.Id},{vendor.UserId},{vendor.BrandName},{vendor.Slug},{vendor.ContactPerson},
                {vendor.Hotline},{vendor.City},{vendor.CommissionRate},{vendor.RatingAvg},{vendor.TotalReviews},
                {vendor.IsActive},{vendor.IsVerified},{vendor.IsTopRated},{vendor.AllowInCombos},
                {vendor.MaxComboDiscount},{vendor.CreatedAt});
            """);
        var category = await db.Categories.FirstAsync();
        var listing = Listing.Create(vendor.Id, category.Id, "Synthetic legacy service", $"review4c-{Guid.NewGuid():N}", 1_000_000, 1_000_000, "Synthetic original service", "HCM");
        listing.Approve(); db.Listings.Add(listing);
        var template = ComboTemplate.Create("Synthetic legacy template", "Popular", "Classic", "Original template", null, 1);
        template.SetScope(vendor.Id, "HCM", 0, true); db.ComboTemplates.Add(template);
        var revision = ComboTemplateRevision.Create(template.Id, 1, "{\"syntheticApprovedRevision\":true}", owner.Id, "VendorOwner", true);
        revision.Review(true, moderator.Id, "Moderator", "Synthetic revision approval"); db.ComboTemplateRevisions.Add(revision);
        BookingContract AddContract(User actor)
        {
            var lead = Lead.Create(actor.Id, vendor.Id, listing.Id, "0900000000", DateTime.UtcNow.AddDays(-10), 100, 1_000_000, "Synthetic legacy lead");
            db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, vendor.Id, actor.Id, 1_000_000, 200_000, null, lead.WeddingDate);
            contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m); contract.CompleteContract();
            db.Entry(contract).Property(row => row.ContractCode).CurrentValue = $"HD-4C-{Guid.NewGuid():N}";
            db.BookingContracts.Add(contract); return contract;
        }
        var contract = AddContract(customer); var verifiedContract = AddContract(otherCustomer);
        var commission = Commission.Create(verifiedContract.Id, vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 40_000, DateTime.UtcNow.Date.AddDays(10));
        commission.ApplyPayment(10_000, "SYNTHETIC-LEGACY-PARTIAL"); db.Commissions.Add(commission);
        db.PaymentTransactions.Add(PaymentTransaction.Create($"SYNTHETIC-{Guid.NewGuid():N}", "Synthetic", "synthetic-account", 10_000, "Synthetic retained payment event"));
        await db.SaveChangesAsync();

        var order = Guid.NewGuid(); var consultation = Guid.NewGuid(); var verified = Guid.NewGuid();
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [ComboOrders] ([Id], [Code], [ComboTemplateId], [CustomerId], [Status], [Subtotal], [DiscountPercent], [DiscountAmount], [Total], [Region], [WeddingDate], [GuestCount], [Note], [CreatedAt])
            VALUES ({order}, {"LEGACY-4C-" + order.ToString("N")[..12]}, {template.Id}, {customer.Id}, {"Completed"}, {1_000_000m}, {0m}, {0m}, {1_000_000m}, {"HCM"}, {created}, {100}, {"Synthetic legacy order without revision evidence"}, {created})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Reviews] ([Id], [BookingContractId], [ListingId], [CustomerId], [VendorId], [Rating], [Content], [PhotosJson], [IsVerifiedBuyer], [Status], [VendorReply], [VendorRepliedAt], [ModeratedAt], [QualityRating], [ServiceRating], [PunctualityRating], [ValueRating], [ModerationFlag], [RejectionReason], [CreatedAt], [UpdatedAt])
            VALUES ({consultation}, NULL, {listing.Id}, {customer.Id}, {vendor.Id}, {4}, {Content}, {"[\"https://synthetic.example.invalid/legacy.jpg\"]"}, {false}, {"Approved"}, {"Synthetic original vendor reply"}, {created}, {created}, {4}, {4}, {5}, {3}, {"Synthetic retained flag"}, NULL, {created}, {created})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Reviews] ([Id], [BookingContractId], [ListingId], [CustomerId], [VendorId], [Rating], [Content], [IsVerifiedBuyer], [Status], [CreatedAt])
            VALUES ({verified}, {verifiedContract.Id}, {listing.Id}, {otherCustomer.Id}, {vendor.Id}, {5}, {Content}, {true}, {"Approved"}, {created})
            """);
        var component = ComboOrderItem.Create(order, listing.Id, vendor.Id, category.Id,
            "Synthetic retained component title", 1_000_000, 0);
        component.AttachLead(contract.LeadId); db.ComboOrderItems.Add(component);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new(customer.Id, vendor.Id, listing.Id, contract.Id, template.Id, revision.Id, order, consultation);
    }

    private static Task<int> InsertReviewAsync(ApplicationDbContext db, Seed seed, Guid id, string target,
        Guid? contract, Guid? order, Guid? template, Guid? listing) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO [Reviews] ([Id], [TargetType], [BookingContractId], [ComboOrderId], [ComboTemplateId], [ListingId], [CustomerId], [VendorId], [Rating], [Content], [IsVerifiedBuyer], [Status], [CreatedAt])
        VALUES ({id}, {target}, {contract}, {order}, {template}, {listing}, {seed.CustomerId}, {seed.VendorId}, {5}, {Content}, {true}, {"Approved"}, {DateTime.UtcNow})
        """);

    private static Task<int> InsertEditAsync(ApplicationDbContext db, Seed seed, Guid id, int attempt, string status) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO [ReviewEditRequests] ([Id], [ReviewId], [AttemptNumber], [RequestedById], [BeforeRating], [BeforeContent], [AfterRating], [AfterContent], [Status], [RequestedAt], [CreatedAt])
        VALUES ({id}, {seed.ConsultationId}, {attempt}, {seed.CustomerId}, {4}, {Content}, {5}, {Content + " Proposed edit."}, {status}, {DateTime.UtcNow}, {DateTime.UtcNow})
        """);

    private static async Task ExpectSqlFailureAsync(Func<Task> action, params int[] expectedNumbers)
    {
        var failure = await Assert.ThrowsAsync<SqlException>(action);
        Assert.Contains(failure.Number, expectedNumbers);
    }

    private static async Task AssertDownRefusedAsync(OwnedDatabase owned, string expectedMessage)
    {
        owned.AssertTarget();
        var failure = await Assert.ThrowsAsync<SqlException>(() => owned.Db.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        Assert.Equal(51000, failure.Number); Assert.Contains(expectedMessage, failure.Message);
        Assert.Equal(ReviewMigration, (await owned.Db.Database.GetAppliedMigrationsAsync()).Last());
    }

    private static async Task AssertSchemaAsync(ApplicationDbContext db)
    {
        var indexes = new Dictionary<string, string?>();
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT [name], [filter_definition] FROM sys.indexes WHERE [object_id] IN (OBJECT_ID(N'dbo.Reviews'), OBJECT_ID(N'dbo.ReviewEditRequests')) AND [is_unique] = 1";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) indexes.Add(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
        }
        foreach (var index in new[] { "IX_Reviews_BookingContractId_ListingId", "IX_Reviews_CustomerId_ListingId", "IX_Reviews_BookingContractId_VendorId", "IX_Reviews_ComboOrderId_VendorId", "IX_Reviews_ComboOrderId_ComboTemplateId", "IX_ReviewEditRequests_ReviewId_Status" })
        { Assert.True(indexes.ContainsKey(index), index); Assert.False(string.IsNullOrWhiteSpace(indexes[index]), index); }
        Assert.Contains("IX_ReviewEditRequests_ReviewId_AttemptNumber", indexes.Keys);
        await using var check = db.Database.GetDbConnection().CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sys.check_constraints WHERE [name] = N'CK_Reviews_TargetReferences' AND [is_disabled] = 0 AND [is_not_trusted] = 0";
        Assert.Equal(1, Convert.ToInt32(await check.ExecuteScalarAsync()));
    }

    private static async Task AssertLaterIdentityColumnAbsentAsync(ApplicationDbContext db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID(N'dbo.Users') AND [name] = N'FacebookSubject'";
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        command.CommandText = """
            SELECT COUNT(*) FROM sys.columns
            WHERE ([object_id]=OBJECT_ID(N'dbo.Users') AND [name]=N'LoginName')
               OR ([object_id]=OBJECT_ID(N'dbo.Vendors') AND [name]=N'OperatingRegions');
            """;
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static async Task<List<TableSnapshot>> CaptureOriginalTablesAsync(ApplicationDbContext db)
    {
        var columns = new Dictionary<string, List<(string Name, int KeyOrdinal)>>();
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = """
                SELECT t.[name], c.[name], COALESCE(k.[key_ordinal], 0)
                FROM sys.tables t JOIN sys.columns c ON c.[object_id] = t.[object_id]
                LEFT JOIN sys.indexes i ON i.[object_id] = t.[object_id] AND i.[is_primary_key] = 1
                LEFT JOIN sys.index_columns k ON k.[object_id] = t.[object_id] AND k.[index_id] = i.[index_id] AND k.[column_id] = c.[column_id]
                WHERE SCHEMA_NAME(t.[schema_id]) = N'dbo' AND t.[name] <> N'__EFMigrationsHistory'
                ORDER BY t.[name], c.[column_id]
                """;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0); var column = reader.GetString(1);
                if (table == "Vendors" && column is "RatingAvg" or "TotalReviews" or "IsTopRated") continue;
                if (!columns.TryGetValue(table, out var list)) columns[table] = list = [];
                list.Add((column, Convert.ToInt32(reader.GetValue(2))));
            }
        }
        var snapshots = new List<TableSnapshot>();
        foreach (var (table, fields) in columns)
        {
            var order = fields.Where(field => field.KeyOrdinal > 0).OrderBy(field => field.KeyOrdinal).Select(field => field.Name).ToArray();
            Assert.NotEmpty(order);
            var snapshot = new TableSnapshot(table, fields.Select(field => field.Name).ToArray(), order, "");
            snapshots.Add(snapshot with { Json = await ReadOriginalRowsAsync(db, snapshot) });
        }
        return snapshots;
    }

    private static async Task AssertOriginalTablesAsync(ApplicationDbContext db, IEnumerable<TableSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
            Assert.True(snapshot.Json == await ReadOriginalRowsAsync(db, snapshot), $"Legacy rows/columns changed in {snapshot.Table}.");
    }

    private static async Task<string> ReadOriginalRowsAsync(ApplicationDbContext db, TableSnapshot snapshot)
    {
        static string Quote(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT {string.Join(", ", snapshot.Columns.Select(Quote))} FROM [dbo].{Quote(snapshot.Table)} ORDER BY {string.Join(", ", snapshot.KeyColumns.Select(Quote))} FOR JSON PATH, INCLUDE_NULL_VALUES";
        var json = new StringBuilder();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) json.Append(reader.GetString(0));
        return json.ToString();
    }

    private sealed record Seed(Guid CustomerId, Guid VendorId, Guid ListingId, Guid ContractId,
        Guid TemplateId, Guid RevisionId, Guid OrderId, Guid ConsultationId);
    private sealed record TableSnapshot(string Table, string[] Columns, string[] KeyColumns, string Json);

    private sealed class OwnedDatabase(string name, ApplicationDbContext db) : IAsyncDisposable
    {
        private const string Prefix = "WeddingPlanTest_Review4C_";
        public ApplicationDbContext Db => db;

        public void AssertTarget()
        {
            var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            Assert.Equal("localhost", target.DataSource, ignoreCase: true);
            Assert.True(target.IntegratedSecurity); Assert.False(target.AttachDBFilename.Length > 0);
            Assert.Equal(name, target.InitialCatalog);
            Assert.StartsWith(Prefix, name); Assert.True(Guid.TryParseExact(name[Prefix.Length..], "N", out _));
        }

        public static async Task<OwnedDatabase> CreateAsync()
        {
            // The environment variable is an opt-in only. Never accept its contents as
            // a server, credential or database connection string.
            Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER")));
            var name = Prefix + Guid.NewGuid().ToString("N");
            var target = new SqlConnectionStringBuilder
            {
                DataSource = "localhost", InitialCatalog = name, IntegratedSecurity = true,
                TrustServerCertificate = true, ConnectTimeout = 5
            };
            var owned = new OwnedDatabase(name, new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(target.ConnectionString).Options));
            owned.AssertTarget();
            var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using (var exists = connection.CreateCommand())
            {
                exists.CommandText = "SELECT DB_ID(@database)";
                exists.Parameters.AddWithValue("@database", name);
                Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
            }
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = $"CREATE DATABASE [{name}]";
                await create.ExecuteNonQueryAsync();
            }
            try { await owned.Db.Database.OpenConnectionAsync(); return owned; }
            catch { await owned.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            AssertTarget();
            try { await db.Database.EnsureDeletedAsync(); }
            finally { await db.DisposeAsync(); }
        }
    }
}
