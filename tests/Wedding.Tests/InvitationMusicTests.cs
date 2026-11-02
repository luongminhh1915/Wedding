using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations;
using Wedding.Application.Features.Invitations.Commands.SaveInvitation;
using Wedding.Application.Features.Invitations.Queries.GetPublicInvitation;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class InvitationMusicTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=Abc_123-XyZ&feature=share")]
    [InlineData("https://youtube.com/watch?v=Abc_123-XyZ")]
    [InlineData("https://m.youtube.com/watch?v=Abc_123-XyZ")]
    [InlineData("https://music.youtube.com/watch?v=Abc_123-XyZ")]
    [InlineData("https://youtu.be/Abc_123-XyZ?si=share")]
    [InlineData("https://www.youtu.be/Abc_123-XyZ")]
    [InlineData("https://www.youtube.com/embed/Abc_123-XyZ")]
    [InlineData("https://youtube.com/shorts/Abc_123-XyZ")]
    [InlineData("https://youtube.com/live/Abc_123-XyZ")]
    [InlineData("https://www.youtube-nocookie.com/embed/Abc_123-XyZ")]
    [InlineData("https://youtube-nocookie.com/embed/Abc_123-XyZ")]
    public void YouTubeSourcesNormalizeToCanonicalVideoReference(string url)
        => Assert.Equal("https://www.youtube.com/watch?v=Abc_123-XyZ", InvitationMusicUrls.Normalize(url, Guid.NewGuid()));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:audio/mpeg;base64,eA==")]
    [InlineData("file:///etc/private.mp3")]
    [InlineData("https://user:pass@example.com/song.mp3")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("http://youtube.com/watch?v=Abc_123-XyZ")]
    [InlineData("https://youtube.com:8443/watch?v=Abc_123-XyZ")]
    [InlineData("https://youtube.com/watch?v=Abc_123-XyZ&v=Zyx_321-CbA")]
    [InlineData("https://youtube.com/watch?v=Abc_123-XyZ&%76=Zyx_321-CbA")]
    [InlineData("https://youtube.com/watch?v=Abc_123-Xy%5A")]
    [InlineData("https://youtube.com/embed/Abc_123-XyZ/extra")]
    [InlineData("https://youtube.com/playlist?list=Abc_123-XyZ")]
    [InlineData("https://youtube-nocookie.com/watch?v=Abc_123-XyZ")]
    [InlineData("https://youtu.be/Abc_123-XyZ/extra")]
    [InlineData("https://youtube.com.evil.invalid/watch?v=Abc_123-XyZ")]
    [InlineData("https://evil-youtube.invalid/watch?v=Abc_123-XyZ")]
    [InlineData("//www.youtube.com/watch?v=Abc_123-XyZ")]
    [InlineData("/uploads/invitation-audio/../../private.mp3")]
    public void InvalidOrSpoofedSourcesNeverFallBackToUntrustedIframe(string url)
        => Assert.Throws<DomainException>(() => InvitationMusicUrls.Normalize(url, Guid.NewGuid()));

    [Fact]
    public void UploadReferencesRequireExactOwnerAndLegacyAudioStillWorks()
    {
        var owner = Guid.NewGuid(); var upload = $"/uploads/invitation-audio/{owner:N}/{Guid.NewGuid():N}.mp3";
        Assert.Equal(upload, InvitationMusicUrls.Normalize(upload, owner));
        Assert.Throws<DomainException>(() => InvitationMusicUrls.Normalize(upload, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => InvitationMusicUrls.Normalize(upload.Replace(".mp3", ".exe"), owner));
        Assert.Throws<DomainException>(() => InvitationMusicUrls.Normalize(new string('a', 501), owner));
        Assert.Equal("https://audio.example.invalid/legacy-track", InvitationMusicUrls.Normalize("https://audio.example.invalid/legacy-track", owner));
        Assert.Equal("http://audio.example.invalid/old.mp3", InvitationMusicUrls.Normalize("http://audio.example.invalid/old.mp3", owner));
        Assert.Null(InvitationMusicUrls.Normalize("", owner)); Assert.Null(InvitationMusicUrls.Normalize(null, owner));
    }

    [Fact]
    public async Task SharedOwnerMusicRoundTripReplacementRemovalAndRejectedSaveArePersistent() => await RoundTrip(sql: false);

    [SqlServerFact]
    public async Task SqlServerOwnedUniqueDatabasePreservesMusicAcrossContextAndSharedEditor() => await RoundTrip(sql: true);

    private static async Task RoundTrip(bool sql)
    {
        await using var fixture = await Fixture.Create(sql); var db = fixture.Db;
        var handler = new SaveInvitationCommandHandler(db, new Current(fixture.Editor.Id));
        var music = $"/uploads/invitation-audio/{fixture.Owner.Id:N}/{Guid.NewGuid():N}.mp3";
        var command = Command(music);
        var saved = await handler.Handle(command, default);
        Assert.Equal(fixture.Owner.Id, saved.CustomerId); Assert.Equal(music, saved.MusicUrl);
        db.ChangeTracker.Clear();
        var publicInvitation = await new GetPublicInvitationQueryHandler(db).Handle(new("audio-roundtrip"), default);
        Assert.Equal(music, publicInvitation.MusicUrl);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command with { MusicUrl = $"/uploads/invitation-audio/{fixture.Editor.Id:N}/{Guid.NewGuid():N}.mp3" }, default));
        db.ChangeTracker.Clear();
        Assert.Equal(music, (await db.WeddingInvitations.AsNoTracking().SingleAsync()).MusicUrl);
        var youtube = await handler.Handle(command with { MusicUrl = "https://youtu.be/Abc_123-XyZ?si=share" }, default);
        Assert.Equal("https://www.youtube.com/watch?v=Abc_123-XyZ", youtube.MusicUrl);
        db.ChangeTracker.Clear();
        await using (var reopened = fixture.Reopen())
            Assert.Equal(youtube.MusicUrl, (await new GetPublicInvitationQueryHandler(reopened).Handle(new("audio-roundtrip"), default)).MusicUrl);
        await handler.Handle(command with { MusicUrl = null }, default);
        db.ChangeTracker.Clear();
        Assert.Null((await new GetPublicInvitationQueryHandler(db).Handle(new("audio-roundtrip"), default)).MusicUrl);
        Assert.Equal(3, await db.Users.CountAsync()); Assert.Single(await db.WeddingInvitations.ToListAsync());
    }

    [Theory]
    [InlineData(UserRole.VendorOwner)]
    [InlineData(UserRole.SuperAdmin)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.CustomerCare)]
    public async Task ForgedCustomerClaimCannotSaveInvitationAsAnotherDbActor(UserRole role)
    {
        await using var fixture = await Fixture.Create();
        var other = User.Create("Synthetic wrong role", $"{Guid.NewGuid():N}@music.example.invalid", "0900000000", "synthetic-no-login", role);
        fixture.Db.Users.Add(other); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new SaveInvitationCommandHandler(fixture.Db, new Current(other.Id)).Handle(Command(null), default));
        Assert.Empty(await fixture.Db.WeddingInvitations.ToListAsync());
    }

    [Fact]
    public async Task InactiveAndViewerCannotSaveOrUploadAndViewerCannotReadUploadStream()
    {
        await using var fixture = await Fixture.Create(); var db = fixture.Db;
        var membership = await db.WeddingMembers.SingleAsync(); membership.SetRole("Viewer"); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new SaveInvitationCommandHandler(db, new Current(fixture.Editor.Id)).Handle(Command(null), default));
        var configuration = new ConfigurationBuilder().Build();
        var controller = new UploadController(new HostEnvironment(), configuration, db, new Current(fixture.Editor.Id));
        var file = new NeverReadFormFile();
        await Assert.ThrowsAsync<DomainException>(() => controller.UploadInvitationAudio(file, default)); Assert.False(file.WasOpened);
        (await db.Users.SingleAsync(user => user.Id == fixture.Owner.Id)).SetStatus(false); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new SaveInvitationCommandHandler(db, new Current(fixture.Owner.Id)).Handle(Command(null), default));
        Assert.Empty(await db.WeddingInvitations.ToListAsync());
        var attribute = typeof(UploadController).GetMethod(nameof(UploadController.UploadInvitationAudio))!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Customer", attribute.Roles);
    }

    [Fact]
    public void PublicAudioRouteReturnsSafeRangeEnabledMp3WithNoSniffHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), "WeddingAudioController_" + Guid.NewGuid().ToString("N"));
        var owner = Guid.NewGuid(); var fileName = $"{Guid.NewGuid():N}.mp3";
        Directory.CreateDirectory(Path.Combine(root, owner.ToString("N")));
        try
        {
            File.WriteAllBytes(Path.Combine(root, owner.ToString("N"), fileName), InvitationAudioStorageTests.Mp3());
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:InvitationAudioRoot"] = root }).Build();
            var controller = new UploadController(new HostEnvironment(), configuration, null!, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
            var result = Assert.IsType<PhysicalFileResult>(controller.GetInvitationAudio(owner, fileName));
            Assert.Equal("audio/mpeg", result.ContentType); Assert.True(result.EnableRangeProcessing);
            Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"]);
            Assert.Equal("public,max-age=31536000,immutable", controller.Response.Headers.CacheControl);
            Assert.IsType<NotFoundResult>(controller.GetInvitationAudio(owner, "../../appsettings.json"));
            Assert.IsType<NotFoundResult>(controller.GetInvitationAudio(Guid.NewGuid(), fileName));
        }
        finally
        {
            var prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "WeddingAudioController_");
            if (!Path.GetFullPath(root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(root[prefix.Length..], "N", out _)) throw new InvalidOperationException("Unsafe test cleanup.");
            Directory.Delete(root, true);
        }
    }

    private static SaveInvitationCommand Command(string? music) => new("audio-roundtrip", "Minh", "Anh", DateTime.UtcNow.AddDays(50), "Synthetic venue", "Synthetic address", null, "Our story", null, music, "navy-cream", null);
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => "synthetic@music.example.invalid"; public string? Role => "Customer"; }
    private sealed class HostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Wedding.WebApi";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class NeverReadFormFile : IFormFile
    {
        public bool WasOpened { get; private set; }
        public string ContentType => "audio/mpeg"; public string ContentDisposition => "";
        public IHeaderDictionary Headers => new HeaderDictionary(); public long Length => 100;
        public string Name => "file"; public string FileName => "song.mp3";
        public Stream OpenReadStream() { WasOpened = true; throw new InvalidOperationException("Authorization must run before reading."); }
        public void CopyTo(Stream target) => throw new NotSupportedException();
        public Task CopyToAsync(Stream target, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Fixture(ApplicationDbContext db, DbContextOptions<ApplicationDbContext> options, SqliteConnection? sqlite, string? databaseName) : IAsyncDisposable
    {
        private const string DatabasePrefix = "WeddingPlanTest_InvitationMusic_";
        public ApplicationDbContext Db { get; } = db;
        public User Owner { get; private set; } = null!;
        public User Editor { get; private set; } = null!;
        public ApplicationDbContext Reopen() => new(options);
        public static async Task<Fixture> Create(bool sql = false)
        {
            SqliteConnection? sqlite = null; string? name = null;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql)
            {
                Assert.Equal("1", Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER"));
                name = DatabasePrefix + Guid.NewGuid().ToString("N");
                var target = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = name, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
                await using var master = new SqlConnection(new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
                await master.OpenAsync();
                await using (var exists = master.CreateCommand())
                {
                    exists.CommandText = "SELECT DB_ID(@database)"; exists.Parameters.AddWithValue("@database", name);
                    Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
                }
                await using (var create = master.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
                options.UseSqlServer(target.ConnectionString);
            }
            else { sqlite = new("Data Source=:memory:"); await sqlite.OpenAsync(); options.UseSqlite(sqlite); }
            var fixture = new Fixture(new ApplicationDbContext(options.Options), options.Options, sqlite, name);
            try
            {
                if (sql) { fixture.AssertOwnedTarget(); await fixture.Db.Database.MigrateAsync(); }
                else await fixture.Db.Database.EnsureCreatedAsync();
                fixture.Owner = User.Create("Synthetic music owner", $"{Guid.NewGuid():N}@music.example.invalid", "0900000000", "synthetic-no-login", UserRole.Customer);
                fixture.Editor = User.Create("Synthetic music editor", $"{Guid.NewGuid():N}@music.example.invalid", "0900000001", "synthetic-no-login", UserRole.Customer);
                var other = User.Create("Synthetic music other", $"{Guid.NewGuid():N}@music.example.invalid", "0900000002", "synthetic-no-login", UserRole.Customer);
                fixture.Db.Users.AddRange(fixture.Owner, fixture.Editor, other);
                var profile = WeddingProfile.Create(fixture.Owner.Id); fixture.Db.WeddingProfiles.Add(profile);
                fixture.Db.WeddingMembers.Add(WeddingMember.Create(profile.Id, fixture.Editor.Id, "Editor")); await fixture.Db.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        private void AssertOwnedTarget()
        {
            Assert.NotNull(databaseName); Assert.StartsWith(DatabasePrefix, databaseName);
            Assert.True(Guid.TryParseExact(databaseName[DatabasePrefix.Length..], "N", out _));
            var target = new SqlConnectionStringBuilder(Db.Database.GetConnectionString());
            Assert.Equal("localhost", target.DataSource, ignoreCase: true); Assert.True(target.IntegratedSecurity);
            Assert.Empty(target.AttachDBFilename); Assert.Equal(databaseName, target.InitialCatalog);
        }
        public async ValueTask DisposeAsync()
        {
            try { if (databaseName != null) { AssertOwnedTarget(); await Db.Database.EnsureDeletedAsync(); } }
            finally { await Db.DisposeAsync(); if (sqlite != null) await sqlite.DisposeAsync(); }
        }
    }
}
