using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Services;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public sealed class BackgroundMusicPlaylistTests
{
    [Fact]
    public async Task PlaylistAndSiteAudioPersistInConfiguredPrivateAudioRootAcrossSiteReplacement()
    {
        using var folder = new TestFolder(); var root = Path.Combine(folder.Path, "private", "uploads", "invitation-audio");
        var oldAudio = new InvitationAudioStorage(Path.Combine(folder.Path, "old-site"), root); var bytes = InvitationAudioStorageTests.Mp3(tags: true);
        var uploaded = await oldAudio.SaveAsync(BackgroundMusicPlaylistStorage.SiteOwnerId, "private-original.mp3", "audio/mpeg", new MemoryStream(bytes), bytes.Length, default);
        using var input = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new { id = "track-one", title = "Test music", type = "audio", url = uploaded.Url, isCustom = true, secret = "must-not-publish" } }));
        var saved = await new BackgroundMusicPlaylistStorage(oldAudio).SaveAsync(input.RootElement, default);
        var nextAudio = new InvitationAudioStorage(Path.Combine(folder.Path, "new-site"), root);
        var read = await new BackgroundMusicPlaylistStorage(nextAudio).ReadAsync(default);
        Assert.Equal(Assert.Single(saved), Assert.Single(read));
        Assert.StartsWith($"/uploads/invitation-audio/{BackgroundMusicPlaylistStorage.SiteOwnerId:N}/", read[0].Url);
        Assert.True(File.Exists(nextAudio.Resolve(BackgroundMusicPlaylistStorage.SiteOwnerId, uploaded.Url.Split('/').Last())));
        Assert.DoesNotContain("secret", await File.ReadAllTextAsync(Path.Combine(root, "site-playlist.json")));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "old-site"))); Assert.False(Directory.Exists(Path.Combine(folder.Path, "new-site")));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"javascript:alert(1)\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"http://audio.example.invalid/music.mp3\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"https://user:pass@audio.example.invalid/music.mp3\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"file:///private/music.mp3\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"blob:preview\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"/uploads/invitation-audio/11111111111111111111111111111111/22222222222222222222222222222222.mp3\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"/uploads/invitation-audio/c8e62673872e40a09bed56b3d42c8ebf/../../web.config\"}]")]
    [InlineData("[{\"id\":\"test\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"https://audio.example.invalid/page.html\"}]")]
    [InlineData("[{\"id\":\"wrong-synth\",\"title\":\"Bad\",\"type\":\"synth\"}]")]
    [InlineData("[{\"id\":\"canon-synth\",\"title\":\"Bad\",\"type\":\"synth\",\"url\":\"https://audio.example.invalid/music.mp3\"}]")]
    public async Task InvalidPlaylistNeverCreatesOrPublishesAFile(string json)
    {
        using var folder = new TestFolder(); var audio = new InvitationAudioStorage(folder.Path); using var input = JsonDocument.Parse(json);
        await Assert.ThrowsAsync<DomainException>(() => new BackgroundMusicPlaylistStorage(audio).SaveAsync(input.RootElement, default));
        Assert.False(Directory.Exists(audio.Root));
    }

    [Fact]
    public async Task ValidYouTubeTrackCanBeSavedAndReadSuccessfully()
    {
        using var folder = new TestFolder();
        var audio = new InvitationAudioStorage(folder.Path);
        var store = new BackgroundMusicPlaylistStorage(audio);
        using var input = JsonDocument.Parse("""[{"id":"yt-wedding","title":"A Thousand Years","type":"youtube","url":"https://www.youtube.com/watch?v=rtOvBOTyX00","isCustom":true}]""");
        var saved = await store.SaveAsync(input.RootElement, default);
        Assert.Single(saved);
        Assert.Equal("yt-wedding", saved[0].Id);
        Assert.Equal("youtube", saved[0].Type);
        Assert.Equal("https://www.youtube.com/watch?v=rtOvBOTyX00", saved[0].Url);

        var read = await store.ReadAsync(default);
        Assert.Single(read);
        Assert.Equal("rtOvBOTyX00", BackgroundMusicPlaylistStorage.ExtractYouTubeId(new Uri(read[0].Url!)));
        Assert.False(read[0].Hidden);
    }

    [Fact]
    public async Task HiddenTrackStateIsPreservedAcrossSaveAndRead()
    {
        using var folder = new TestFolder();
        var audio = new InvitationAudioStorage(folder.Path);
        var store = new BackgroundMusicPlaylistStorage(audio);
        using var input = JsonDocument.Parse("""[{"id":"canon-synth","title":"Hidden Synth","type":"synth","hidden":true}]""");
        var saved = await store.SaveAsync(input.RootElement, default);
        Assert.Single(saved);
        Assert.True(saved[0].Hidden);

        var read = await store.ReadAsync(default);
        Assert.Single(read);
        Assert.Equal("Hidden Synth", read[0].Title);
        Assert.True(read[0].Hidden);
    }

    [Fact]
    public async Task CountDuplicateIdTitleBoundsAndMissingManagedFileAreRejectedWithoutChangingPublishedState()
    {
        using var folder = new TestFolder(); var audio = new InvitationAudioStorage(folder.Path); var store = new BackgroundMusicPlaylistStorage(audio);
        using var original = JsonDocument.Parse("[{\"id\":\"canon-synth\",\"title\":\"Original\",\"type\":\"synth\"}]");
        await store.SaveAsync(original.RootElement, default); var path = Path.Combine(audio.Root, "site-playlist.json"); var before = await File.ReadAllBytesAsync(path);
        var invalid = new[]
        {
            JsonSerializer.Serialize(Enumerable.Range(0, 21).Select(i => new { id = $"audio-{i}", title = "Audio", type = "audio", url = "https://music.example.invalid/song.mp3" })),
            "[{\"id\":\"canon-synth\",\"title\":\"One\",\"type\":\"synth\"},{\"id\":\"canon-synth\",\"title\":\"Two\",\"type\":\"synth\"}]",
            JsonSerializer.Serialize(new[] { new { id = "audio", title = new string('x', 151), type = "audio", url = "https://music.example.invalid/song.mp3" } }),
            "[{\"id\":\"missing\",\"title\":\"Missing\",\"type\":\"audio\",\"url\":\"/uploads/invitation-audio/c8e62673872e40a09bed56b3d42c8ebf/22222222222222222222222222222222.mp3\"}]"
        };
        foreach (var json in invalid)
        {
            using var input = JsonDocument.Parse(json); await Assert.ThrowsAsync<DomainException>(() => store.SaveAsync(input.RootElement, default));
            Assert.Equal(before, await File.ReadAllBytesAsync(path));
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(original.RootElement, cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(path)); Assert.Empty(Directory.EnumerateFiles(audio.Root, "*.tmp"));
    }

    [Fact]
    public async Task InvalidOrOversizedHistoricalJsonIsPreservedButNeverReturnedToPublicReaders()
    {
        using var folder = new TestFolder(); var audio = new InvitationAudioStorage(folder.Path); var store = new BackgroundMusicPlaylistStorage(audio);
        Directory.CreateDirectory(audio.Root); var path = Path.Combine(audio.Root, "site-playlist.json");
        foreach (var json in new[] { "{invalid secret}", new string('x', BackgroundMusicPlaylistStorage.MaximumJsonBytes + 1), "[{\"id\":\"bad\",\"title\":\"Bad\",\"type\":\"audio\",\"url\":\"javascript:alert(1)\"}]" })
        {
            await File.WriteAllTextAsync(path, json); var publicValue = await store.ReadAsync(default);
            Assert.Equal("canon-synth", publicValue[0].Id); Assert.Equal(json, await File.ReadAllTextAsync(path));
            Assert.DoesNotContain("secret", JsonSerializer.Serialize(publicValue));
        }
    }

    [Fact]
    public async Task ParallelReadersSeeOnlyCompleteOldOrNewPlaylists()
    {
        using var folder = new TestFolder(); var store = new BackgroundMusicPlaylistStorage(new InvitationAudioStorage(folder.Path));
        using var first = JsonDocument.Parse("[{\"id\":\"canon-synth\",\"title\":\"One\",\"type\":\"synth\"}]");
        using var second = JsonDocument.Parse("[{\"id\":\"canon-synth\",\"title\":\"Two\",\"type\":\"synth\"}]");
        await store.SaveAsync(first.RootElement, default);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            if (i % 2 == 0) await store.SaveAsync(second.RootElement, default);
            else Assert.Contains(Assert.Single(await store.ReadAsync(default)).Title, new[] { "One", "Two" });
        }));
    }

    [Fact]
    public void PlaylistWritesAndUploadsAreStaffOnlyWhilePlaylistReadIsExplicitlyPublic()
    {
        foreach (var method in new[] { nameof(UploadController.UploadBackgroundMusic), nameof(UploadController.UpdateBackgroundPlaylist) })
        {
            var info = typeof(UploadController).GetMethod(method)!;
            Assert.Empty(info.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
            Assert.Equal("Moderator,SuperAdmin", Assert.Single(info.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        }
        Assert.Single(typeof(UploadController).GetMethod(nameof(UploadController.GetBackgroundPlaylist))!.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
    }

    [SqlServerFact]
    public async Task MusicWritesRecheckPersistedActiveStaffRoleBeforeReadingUploadOrWritingPlaylist()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false); await owned.Db.Database.EnsureCreatedAsync();
        using var folder = new TestFolder();
        using var payload = JsonDocument.Parse("[{\"id\":\"canon-synth\",\"title\":\"Site track\",\"type\":\"synth\"}]");
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var user = User.Create("QA music " + role, $"{role}@music.example.invalid", "0900000881", "qa-hash", role);
            owned.Db.Users.Add(user); await owned.Db.SaveChangesAsync();
            var controller = Controller(folder.Path, owned.Db, new Current(user.Id, role.ToString()));
            if (role is UserRole.Moderator or UserRole.SuperAdmin)
            {
                var response = Assert.IsType<OkObjectResult>(await controller.UpdateBackgroundPlaylist(payload.RootElement, default));
                var value = JsonSerializer.SerializeToElement(response.Value); Assert.True(value.GetProperty("success").GetBoolean()); Assert.Single(value.GetProperty("tracks").EnumerateArray());
                var mp3 = InvitationAudioStorageTests.Mp3();
                var uploaded = Assert.IsType<OkObjectResult>(await controller.UploadBackgroundMusic(new FormFile(new MemoryStream(mp3), 0, mp3.Length, "file", "song.mp3") { Headers = new HeaderDictionary(), ContentType = "audio/mpeg" }, default));
                Assert.StartsWith($"/uploads/invitation-audio/{BackgroundMusicPlaylistStorage.SiteOwnerId:N}/", JsonSerializer.SerializeToElement(uploaded.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("url").GetString());
            }
            else
            {
                var denied = new NeverReadFile(); await Assert.ThrowsAsync<DomainException>(() => controller.UploadBackgroundMusic(denied, default)); Assert.False(denied.Opened);
                await Assert.ThrowsAsync<DomainException>(() => controller.UpdateBackgroundPlaylist(payload.RootElement, default));
                // A stale privileged claim does not promote a real nonstaff account.
                await Assert.ThrowsAsync<DomainException>(() => Controller(folder.Path, owned.Db, new Current(user.Id, "SuperAdmin")).UpdateBackgroundPlaylist(payload.RootElement, default));
            }
            user.SetStatus(false); await owned.Db.SaveChangesAsync();
            var inactive = new NeverReadFile(); await Assert.ThrowsAsync<DomainException>(() => controller.UploadBackgroundMusic(inactive, default)); Assert.False(inactive.Opened);
            await Assert.ThrowsAsync<DomainException>(() => controller.UpdateBackgroundPlaylist(payload.RootElement, default));
        }
        var anonymous = Controller(folder.Path, owned.Db, new Current(null, null));
        Assert.IsType<OkObjectResult>(await anonymous.GetBackgroundPlaylist(default)); Assert.Equal("no-store", anonymous.Response.Headers.CacheControl.ToString());
        await Assert.ThrowsAsync<DomainException>(() => anonymous.UpdateBackgroundPlaylist(payload.RootElement, default));
    }

    private static UploadController Controller(string root, IApplicationDbContext db, ICurrentUserService actor) => new(new Host(root),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:InvitationAudioRoot"] = Path.Combine(root, "private-audio") }).Build(), db, actor)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Host(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "QA"; public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot"); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class NeverReadFile : IFormFile
    {
        public bool Opened { get; private set; } public string ContentType => "audio/mpeg"; public string ContentDisposition => ""; public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => 100; public string Name => "file"; public string FileName => "song.mp3";
        public Stream OpenReadStream() { Opened = true; throw new InvalidOperationException("Denied upload must not be read"); }
        public void CopyTo(Stream target) => throw new NotSupportedException(); public Task CopyToAsync(Stream target, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class TestFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingSiteMusic_" + Guid.NewGuid().ToString("N"));
        public void Dispose()
        {
            var prefix = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), "WeddingSiteMusic_"); var path = System.IO.Path.GetFullPath(Path);
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(path[prefix.Length..], "N", out _)) throw new InvalidOperationException("Unsafe test cleanup");
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
