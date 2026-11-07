using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Services;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public sealed class AppearanceSettingsTests
{
    [Fact]
    public async Task MissingFileReturnsDefaultsWithoutCreatingFilesOrEnablingAutoplay()
    {
        using var folder = new OwnedFolder();
        var audioRoot = Path.Combine(folder.Root, "private", "audio");
        var storage = new AppearanceSettingsStorage(new InvitationAudioStorage(Path.Combine(folder.Root, "site1"), audioRoot));
        Assert.Equal(new AppearanceSettings(), await storage.ReadAsync(default));
        Assert.False(Directory.Exists(audioRoot));
    }

    [Fact]
    public async Task ValidSettingsPersistAcrossApplicationReplacementInConfiguredPrivateRoot()
    {
        using var folder = new OwnedFolder();
        var audioRoot = Path.Combine(folder.Root, "private", "audio");
        var storage = new AppearanceSettingsStorage(new InvitationAudioStorage(Path.Combine(folder.Root, "old-site"), audioRoot));
        using var payload = JsonDocument.Parse("""{"petalsEnabled":false,"petalsStyle":"mixed","petalsSpeed":"slow","petalsDensity":6,"defaultMusicVolume":0,"musicAutoPlay":false}""");
        var saved = await storage.SaveAsync(payload.RootElement, default);
        var replacement = new AppearanceSettingsStorage(new InvitationAudioStorage(Path.Combine(folder.Root, "new-site"), audioRoot));
        Assert.Equal(saved, await replacement.ReadAsync(default));
        Assert.False(saved.PetalsEnabled);
        Assert.Equal(0d, saved.DefaultMusicVolume);
        Assert.False(saved.MusicAutoPlay);
        Assert.False(Directory.Exists(Path.Combine(folder.Root, "old-site")));
        Assert.False(Directory.Exists(Path.Combine(folder.Root, "new-site")));
        Assert.Equal("site-appearance.json", Assert.Single(Directory.GetFiles(audioRoot)).Split(Path.DirectorySeparatorChar).Last());
        using var persisted = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(audioRoot, "site-appearance.json")));
        Assert.Equal(6, persisted.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task PartialLegacyObjectUsesTypedDefaultsAndKeepsSourceFileUnchangedOnRead()
    {
        using var folder = new OwnedFolder();
        var file = Path.Combine(folder.Root, "site-appearance.json");
        const string legacy = """{"petalsDensity":24,"defaultMusicVolume":1}""";
        await File.WriteAllTextAsync(file, legacy);
        var result = await Storage(folder).ReadAsync(default);
        Assert.Equal(24, result.PetalsDensity);
        Assert.Equal(1d, result.DefaultMusicVolume);
        Assert.Equal("blush", result.PetalsStyle);
        Assert.False(result.MusicAutoPlay);
        Assert.Equal(legacy, await File.ReadAllTextAsync(file));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"secret\":\"must-not-be-public\"}")]
    [InlineData("{\"petalsDensity\":14,\"petalsDensity\":12}")]
    [InlineData("{\"petalsEnabled\":\"false\"}")]
    [InlineData("{\"musicAutoPlay\":1}")]
    [InlineData("{\"petalsStyle\":\"<script>\"}")]
    [InlineData("{\"petalsStyle\":\" blush\"}")]
    [InlineData("{\"petalsSpeed\":\"faster\"}")]
    [InlineData("{\"petalsDensity\":5}")]
    [InlineData("{\"petalsDensity\":7}")]
    [InlineData("{\"petalsDensity\":25}")]
    [InlineData("{\"petalsDensity\":14.5}")]
    [InlineData("{\"petalsDensity\":\"14\"}")]
    [InlineData("{\"defaultMusicVolume\":-0.1}")]
    [InlineData("{\"defaultMusicVolume\":1.1}")]
    [InlineData("{\"defaultMusicVolume\":1e999}")]
    [InlineData("{\"defaultMusicVolume\":\"0.35\"}")]
    public async Task InvalidSettingsCannotReplaceThePreviouslySavedFile(string json)
    {
        using var folder = new OwnedFolder();
        var file = Path.Combine(folder.Root, "site-appearance.json");
        const string original = "{\"petalsDensity\":12}";
        await File.WriteAllTextAsync(file, original);
        using var payload = JsonDocument.Parse(json);
        await Assert.ThrowsAsync<DomainException>(() => Storage(folder).SaveAsync(payload.RootElement, default));
        Assert.Equal(original, await File.ReadAllTextAsync(file));
        Assert.Single(Directory.GetFiles(folder.Root));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"privateValue\":\"do-not-serve\"}")]
    [InlineData("{\"defaultMusicVolume\":1e999}")]
    public async Task CorruptOrUnsafeLegacyFileFallsBackWithoutEchoingOrOverwriting(string text)
    {
        using var folder = new OwnedFolder();
        var file = Path.Combine(folder.Root, "site-appearance.json");
        await File.WriteAllTextAsync(file, text);
        Assert.Equal(new AppearanceSettings(), await Storage(folder).ReadAsync(default));
        Assert.Equal(text, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task OversizedLegacyFileIsPreservedAndOversizedInputIsRejected()
    {
        using var folder = new OwnedFolder();
        var file = Path.Combine(folder.Root, "site-appearance.json");
        var oversized = new string('x', AppearanceSettingsStorage.MaximumJsonBytes + 1);
        await File.WriteAllTextAsync(file, oversized);
        Assert.Equal(new AppearanceSettings(), await Storage(folder).ReadAsync(default));
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(new { petalsStyle = oversized }));
        await Assert.ThrowsAsync<DomainException>(() => Storage(folder).SaveAsync(payload.RootElement, default));
        Assert.Equal(oversized, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task CancelledSaveNeverCreatesStorageAndConcurrentSavesLeaveOneCompleteTypedFile()
    {
        using var folder = new OwnedFolder();
        var audioRoot = Path.Combine(folder.Root, "private-audio");
        var storage = new AppearanceSettingsStorage(new InvitationAudioStorage(folder.Root, audioRoot));
        using var payload = JsonDocument.Parse("{}");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(payload.RootElement, cancel.Token));
        Assert.False(Directory.Exists(audioRoot));
        var saved = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            using var input = JsonDocument.Parse(JsonSerializer.Serialize(new { petalsDensity = 6 + i % 10 * 2, defaultMusicVolume = i / 12d }));
            return await storage.SaveAsync(input.RootElement, default);
        }));
        Assert.Contains(await storage.ReadAsync(default), saved);
        Assert.Single(Directory.GetFiles(audioRoot));
    }

    [Fact]
    public void AppearanceWriteKeepsExistingStaffAuthorizationAndBoundedRequest()
    {
        var read = typeof(UploadController).GetMethod(nameof(UploadController.GetAppearanceSettings))!;
        Assert.NotNull(read.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Single());
        var write = typeof(UploadController).GetMethod(nameof(UploadController.UpdateAppearanceSettings))!;
        Assert.Equal("Moderator,SuperAdmin", ((AuthorizeAttribute)write.GetCustomAttributes(typeof(AuthorizeAttribute), false).Single()).Roles);
        var limit = write.CustomAttributes.Single(attribute => attribute.AttributeType == typeof(RequestSizeLimitAttribute));
        Assert.Equal((long)AppearanceSettingsStorage.MaximumJsonBytes, Assert.IsType<long>(limit.ConstructorArguments[0].Value));
    }

    private static AppearanceSettingsStorage Storage(OwnedFolder folder) => new(new InvitationAudioStorage(folder.Root, folder.Root));

    private sealed class OwnedFolder : IDisposable
    {
        private readonly string owner = "WeddingAppearance_" + Guid.NewGuid().ToString("N");
        private readonly string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        public string Root { get; }
        public OwnedFolder()
        {
            Root = Path.GetFullPath(Path.Combine(temporaryRoot, owner));
            Directory.CreateDirectory(Root);
        }
        public void Dispose()
        {
            if (Path.GetDirectoryName(Root) != temporaryRoot || Path.GetFileName(Root) != owner)
                throw new InvalidOperationException("Refusing cleanup outside the owned temporary folder.");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
