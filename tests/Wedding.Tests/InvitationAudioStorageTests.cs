using System.Text;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class InvitationAudioStorageTests
{
    [Fact]
    public async Task SafeGeneratedPathStripsPrivateTagsAndOldFilesRemainOnReplacement()
    {
        using var folder = new TempFolder();
        var storage = new InvitationAudioStorage(folder.Path); var owner = Guid.NewGuid();
        var source = Mp3(tags: true);
        var first = await storage.SaveAsync(owner, "../../private-song.MP3", "audio/mpeg", new MemoryStream(source), source.Length, default);
        var second = await storage.SaveAsync(owner, "replacement.mp3", "audio/mpeg", new MemoryStream(source), source.Length, default);
        Assert.StartsWith($"/uploads/invitation-audio/{owner:N}/", first.Url);
        Assert.DoesNotContain("private", first.Url); Assert.NotEqual(first.Url, second.Url);
        var path = storage.Resolve(owner, first.Url.Split('/').Last()); Assert.NotNull(path);
        var saved = await File.ReadAllBytesAsync(path!);
        Assert.Equal(Mp3(), saved); Assert.Equal(saved.Length, first.Size);
        Assert.DoesNotContain("private", Encoding.ASCII.GetString(saved));
        Assert.True(File.Exists(storage.Resolve(owner, second.Url.Split('/').Last())));
        Assert.False(File.Exists(storage.Resolve(Guid.NewGuid(), first.Url.Split('/').Last())));
        Assert.Null(storage.Resolve(owner, "../../appsettings.json"));
        Assert.Null(storage.Resolve(owner, $"{Guid.NewGuid():N}.exe"));
        Assert.Null(storage.Resolve(Guid.Empty, first.Url.Split('/').Last()));
        Assert.Equal(Path.Combine(folder.Path, "App_Data", "invitation-audio"), storage.Root);
    }

    [Theory]
    [InlineData("song.wav", "audio/mpeg")]
    [InlineData("song.mp3", "text/html")]
    [InlineData("song.mp3.exe", "audio/mpeg")]
    public async Task RejectsFilenameOrMimeMismatchWithoutWrites(string filename, string mime)
    {
        using var folder = new TempFolder(); var storage = new InvitationAudioStorage(folder.Path); var bytes = Mp3();
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(Guid.NewGuid(), filename, mime, new MemoryStream(bytes), bytes.Length, default));
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RejectsEmptyOversizedDeclaredAndActualStreamsLengthMismatchAndInvalidOwner()
    {
        using var folder = new TempFolder(); var storage = new InvitationAudioStorage(folder.Path); var owner = Guid.NewGuid(); var bytes = Mp3();
        Task Upload(byte[] b, long length, Guid? id = null) => storage.SaveAsync(id ?? owner, "song.mp3", "audio/mpeg", new MemoryStream(b), length, default);
        await Assert.ThrowsAsync<DomainException>(() => Upload([], 0));
        await Assert.ThrowsAsync<DomainException>(() => Upload([], InvitationAudioStorage.MaxFileBytes + 1));
        await Assert.ThrowsAsync<DomainException>(() => Upload(new byte[InvitationAudioStorage.MaxFileBytes + 1], 1));
        await Assert.ThrowsAsync<DomainException>(() => Upload(bytes, bytes.Length - 1));
        await Assert.ThrowsAsync<DomainException>(() => Upload(bytes, bytes.Length, Guid.Empty));
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void RejectsId3OnlyFakeTruncatedTrailingScriptsAndReservedHeaders()
    {
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize("ID3\x04\0\0\0\0\0\0"u8.ToArray()));
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize(Encoding.UTF8.GetBytes("<script>not mp3</script>")));
        var bytes = Mp3();
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize(bytes[..^1]));
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize(bytes[..417])); // One frame is not enough evidence.
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize([.. bytes, .. "<script>bad</script>"u8]));
        var bad = bytes.ToArray(); bad[2] |= 0xf0;
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize(bad));
        var id3 = Mp3(tags: true); id3[6] = 0xff;
        Assert.Throws<DomainException>(() => Mp3AudioValidation.Sanitize(id3));
    }

    [Fact]
    public async Task FileCountAndByteQuotasRejectNewUploadWithoutRemovingExisting()
    {
        using var folder = new TempFolder(); var storage = new InvitationAudioStorage(folder.Path); var owner = Guid.NewGuid(); var bytes = Mp3();
        var directory = Path.Combine(storage.Root, owner.ToString("N")); Directory.CreateDirectory(directory);
        for (var n = 0; n < InvitationAudioStorage.OwnerMaxFiles; n++)
            await File.WriteAllBytesAsync(Path.Combine(directory, $"{Guid.NewGuid():N}.mp3"), bytes);
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "new.mp3", "audio/mpeg", new MemoryStream(bytes), bytes.Length, default));
        Assert.Equal(InvitationAudioStorage.OwnerMaxFiles, Directory.EnumerateFiles(directory).Count());
        var other = Guid.NewGuid(); var otherDirectory = Path.Combine(storage.Root, other.ToString("N")); Directory.CreateDirectory(otherDirectory);
        await using (var file = File.Create(Path.Combine(otherDirectory, "existing.mp3"))) file.SetLength(InvitationAudioStorage.OwnerQuotaBytes);
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(other, "new.mp3", "audio/mpeg", new MemoryStream(bytes), bytes.Length, default));
        Assert.Single(Directory.EnumerateFiles(otherDirectory));
    }

    [Fact]
    public async Task CancelledWriteLeavesNoPartialFile()
    {
        using var folder = new TempFolder(); var storage = new InvitationAudioStorage(folder.Path); var bytes = Mp3();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync(Guid.NewGuid(), "song.mp3", "audio/mpeg", new MemoryStream(bytes), bytes.Length, cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories));
    }

    internal static byte[] Mp3(bool tags = false)
    {
        var frame = new byte[417]; frame[0] = 0xff; frame[1] = 0xfb; frame[2] = 0x90; frame[3] = 0; // MPEG1 LayerIII 128kbps/44100Hz, no padding.
        var audio = frame.Concat(frame).Concat(frame).ToArray();
        if (!tags) return audio;
        var metadata = Encoding.UTF8.GetBytes("private artist / location");
        var header = new byte[] { (byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, (byte)metadata.Length };
        var footer = new byte[128]; Encoding.ASCII.GetBytes("TAGprivate song").CopyTo(footer, 0);
        return [.. header, .. metadata, .. audio, .. footer];
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingAudioTest_" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var target = System.IO.Path.GetFullPath(Path);
            var prefix = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), "WeddingAudioTest_");
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(target[prefix.Length..], "N", out _)) throw new InvalidOperationException("Unsafe test directory cleanup.");
            Directory.Delete(target, true);
        }
    }
}
