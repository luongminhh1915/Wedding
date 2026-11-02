using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Wedding.LocalDataBackup;

namespace Wedding.Tests;

public class LocalDataBackupTests
{
    [Fact]
    public void OnlineSnapshotIncludesCommittedWalButNotUncommittedRowsAndLeavesSourceBytesAlone()
    {
        using var fixture = new Fixture();
        using var writer = fixture.Writer();
        Execute(writer, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE Users (Id TEXT PRIMARY KEY, PrivateValue TEXT); INSERT INTO Users VALUES ('1', 'synthetic-retained');");
        Execute(writer, "INSERT INTO Users VALUES ('2', 'synthetic-in-wal');");
        Assert.True(File.Exists(fixture.Source + "-wal"));
        using var pending = writer.BeginTransaction();
        using (var insert = writer.CreateCommand())
        {
            insert.Transaction = pending;
            insert.CommandText = "INSERT INTO Users VALUES ('3', 'not-committed');";
            insert.ExecuteNonQuery();
        }
        var originalMain = HashLiveFile(fixture.Source);
        var originalWal = HashLiveFile(fixture.Source + "-wal");
        var result = SqlitePreservationBackup.Create(new(fixture.Source, fixture.Output));
        Assert.Equal(originalMain, HashLiveFile(fixture.Source));
        Assert.Equal(originalWal, HashLiveFile(fixture.Source + "-wal"));
        Assert.Equal(2, result.TableCounts["Users"]);
        Assert.Equal(result.DatabaseSha256, SqlitePreservationBackup.Sha256(result.DatabasePath));
        Assert.False(File.Exists(result.DatabasePath + "-wal"));
        Assert.False(File.Exists(result.DatabasePath + "-shm"));
        using (var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = result.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString))
        {
            snapshot.Open();
            using var query = snapshot.CreateCommand();
            query.CommandText = "SELECT PrivateValue FROM Users ORDER BY Id;";
            using var rows = query.ExecuteReader();
            Assert.True(rows.Read()); Assert.Equal("synthetic-retained", rows.GetString(0));
            Assert.True(rows.Read()); Assert.Equal("synthetic-in-wal", rows.GetString(0));
            Assert.False(rows.Read());
        }
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.BackupDirectory, "manifest.json")));
        Assert.True(manifest.RootElement.GetProperty("sourceOpenedReadOnly").GetBoolean());
        Assert.Equal("ok", manifest.RootElement.GetProperty("quickCheck").GetString());
        Assert.DoesNotContain("synthetic-retained", manifest.RootElement.GetRawText());
        Assert.DoesNotContain("synthetic-in-wal", manifest.RootElement.GetRawText());
        pending.Rollback();
    }

    [Fact]
    public void UploadArchivePreservesFoldersAndRepeatedBackupsNeverOverwrite()
    {
        using var fixture = new Fixture();
        using var writer = fixture.Writer();
        Execute(writer, "CREATE TABLE [Odd\"Table] (Id TEXT); INSERT INTO [Odd\"Table] VALUES ('retained');");
        var invitations = Directory.CreateDirectory(Path.Combine(fixture.Root, "invitations"));
        var listings = Directory.CreateDirectory(Path.Combine(fixture.Root, "legacy-listings"));
        File.WriteAllText(Path.Combine(invitations.FullName, "image.png"), "synthetic-invitation-image");
        File.WriteAllText(Path.Combine(listings.FullName, "image.png"), "synthetic-listing-image");
        var request = new LocalBackupRequest(fixture.Source, fixture.Output,
        [new(invitations.FullName, "App_Data/invitation-uploads"), new(listings.FullName, "wwwroot/uploads/listings")]);
        var first = SqlitePreservationBackup.Create(request);
        var second = SqlitePreservationBackup.Create(request);
        Assert.NotEqual(first.BackupDirectory, second.BackupDirectory);
        Assert.Equal(first.DatabaseSha256, SqlitePreservationBackup.Sha256(first.DatabasePath));
        Assert.Equal(1, first.TableCounts["Odd\"Table"]);
        Assert.Equal(2, first.UploadFileCount);
        using var zip = ZipFile.OpenRead(first.UploadArchivePath!);
        Assert.Equal(["App_Data/invitation-uploads/image.png", "wwwroot/uploads/listings/image.png"], zip.Entries.Select(entry => entry.FullName).Order().ToArray());
        Assert.Equal(first.UploadArchiveSha256, SqlitePreservationBackup.Sha256(first.UploadArchivePath!));
    }

    [Fact]
    public void MissingSourceAndRecursiveOrTraversingUploadDestinationsAreRejected()
    {
        using var fixture = new Fixture();
        Assert.Throws<FileNotFoundException>(() => SqlitePreservationBackup.Create(new(fixture.Source, fixture.Output)));
        Assert.False(File.Exists(fixture.Source));
        using var writer = fixture.Writer();
        Execute(writer, "CREATE TABLE Users (Id TEXT);");
        Assert.Throws<ArgumentException>(() => SqlitePreservationBackup.Create(new(fixture.Source, fixture.Output, [new(fixture.Root, "App_Data")] )));
        Assert.Throws<ArgumentException>(() => SqlitePreservationBackup.Create(new(fixture.Source, fixture.Output, [new(fixture.Root, "../outside")] )));
    }

    private static void Execute(SqliteConnection database, string sql)
    {
        using var command = database.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }

    private static string HashLiveFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("WeddingLocalBackupTest_").FullName;
        public string Source => Path.Combine(Root, "source.db");
        public string Output => Path.Combine(Root, "backups");
        public SqliteConnection Writer()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Source, Pooling = false }.ConnectionString);
            connection.Open(); return connection;
        }
        public void Dispose()
        {
            Assert.StartsWith("WeddingLocalBackupTest_", Path.GetFileName(Root));
            Directory.Delete(Root, recursive: true); // Exact unique fixture, never application data.
        }
    }
}
