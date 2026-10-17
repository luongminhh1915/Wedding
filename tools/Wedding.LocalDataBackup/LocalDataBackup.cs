using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Wedding.LocalDataBackup;

public sealed record UploadBackupRoot(string SourceDirectory, string ArchiveDirectory);
public sealed record LocalBackupRequest(string SourceDatabase, string OutputDirectory, IReadOnlyList<UploadBackupRoot>? UploadRoots = null);
public sealed record LocalBackupResult(string BackupDirectory, string DatabasePath, string DatabaseSha256,
    IReadOnlyDictionary<string, long> TableCounts, string? UploadArchivePath, string? UploadArchiveSha256, int UploadFileCount);

/// <summary>
/// A preservation tool only: never opens the source writable, starts the API, creates SQL Server
/// databases, imports data, or executes any source schema/application migration.
/// </summary>
public static class SqlitePreservationBackup
{
    public static LocalBackupResult Create(LocalBackupRequest request)
    {
        var source = Path.GetFullPath(request.SourceDatabase);
        var output = Path.GetFullPath(request.OutputDirectory);
        if (!File.Exists(source)) throw new FileNotFoundException("The explicit source SQLite database does not exist; no database was created.", source);
        var roots = request.UploadRoots ?? [];
        foreach (var root in roots)
        {
            var prefix = root.ArchiveDirectory.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(prefix) || Path.IsPathRooted(prefix) || prefix.Split('/').Any(part => part is "." or ".."))
                throw new ArgumentException("Upload archive folders must be safe relative paths.");
            if (IsInside(output, Path.GetFullPath(root.SourceDirectory)))
                throw new ArgumentException("The backup output cannot be placed inside an upload source folder.");
        }

        var directory = Path.Combine(output, $"sqlite-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}Z-{Guid.NewGuid():N}");
        if (Directory.Exists(directory)) throw new IOException("Refusing to reuse an existing backup directory.");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "WeddingPlatformDb.snapshot.db");
        // Reserve a unique destination with CreateNew, never truncate/overwrite any existing file.
        using (new FileStream(databasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        try
        {
            using (var sourceConnection = Open(source, SqliteOpenMode.ReadOnly))
            using (var destination = Open(databasePath, SqliteOpenMode.ReadWrite))
            {
                // Native SQLite online backup reads a consistent snapshot, including committed WAL
                // records. Copying source.db as a file would silently miss those records.
                sourceConnection.BackupDatabase(destination);
                using var journal = destination.CreateCommand();
                journal.CommandText = "PRAGMA journal_mode=DELETE;";
                journal.ExecuteScalar(); // Make the destination a self-contained, closed DB file.
            }

            SortedDictionary<string, long> counts;
            using (var verify = Open(databasePath, SqliteOpenMode.ReadOnly))
            {
                using var check = verify.CreateCommand();
                check.CommandText = "PRAGMA quick_check;";
                using (var integrityRows = check.ExecuteReader())
                {
                    var successful = false;
                    while (integrityRows.Read())
                    {
                        if (!string.Equals(integrityRows.GetString(0), "ok", StringComparison.Ordinal))
                            throw new InvalidDataException("The backup failed SQLite quick_check; it must not be used for recovery.");
                        successful = true;
                    }
                    if (!successful) throw new InvalidDataException("SQLite quick_check did not confirm integrity.");
                }
                counts = ReadTableCounts(verify);
            }

            string? archivePath = null;
            string? archiveHash = null;
            var files = 0;
            if (roots.Count > 0)
            {
                archivePath = Path.Combine(directory, "local-uploads.zip");
                using (var archiveFile = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(archiveFile, ZipArchiveMode.Create))
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var root in roots)
                    {
                        var rootPath = Path.GetFullPath(root.SourceDirectory);
                        if (!Directory.Exists(rootPath)) continue;
                        if ((File.GetAttributes(rootPath) & FileAttributes.ReparsePoint) != 0)
                            throw new IOException("Upload source roots cannot be junctions or symbolic links.");
                        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
                        foreach (var file in Directory.EnumerateFiles(rootPath, "*", options))
                        {
                            var relative = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
                            var entryName = $"{root.ArchiveDirectory.Replace('\\', '/').Trim('/')}/{relative}";
                            if (!names.Add(entryName)) throw new IOException("Upload archive folders overlap; refusing ambiguous backup entries.");
                            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var entryStream = entry.Open();
                            input.CopyTo(entryStream);
                            files++;
                        }
                    }
                }
                archiveHash = Sha256(archivePath);
            }

            var result = new LocalBackupResult(directory, databasePath, Sha256(databasePath), counts, archivePath, archiveHash, files);
            var manifestPath = Path.Combine(directory, "manifest.json");
            using (var manifest = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(manifest, new
                {
                    formatVersion = 1, createdAtUtc = DateTime.UtcNow, sourceDatabase = source,
                    sourceOpenedReadOnly = true, method = "SQLite online BackupDatabase (includes committed WAL)", quickCheck = "ok",
                    result.DatabasePath, result.DatabaseSha256, result.TableCounts,
                    result.UploadArchivePath, result.UploadArchiveSha256, result.UploadFileCount,
                    uploadConsistency = "Files are copied while readable; concurrent upload changes are not transactionally synchronized with the SQLite snapshot.",
                    privacy = "Contains user data. Keep offline/private; never publish, commit, or share the backup. No migration to SQL Server was performed."
                }, new JsonSerializerOptions { WriteIndented = true });
            // A manifest only exists after all checks/archive writes succeed. Failed attempts remain
            // for inspection but are deliberately not labelled a successful backup.
            return result;
        }
        catch
        {
            // Preserve partial artifacts for diagnostics instead of deleting anything material.
            throw new IOException($"Backup incomplete. The source was opened read-only and has not been changed. Partial artifacts: {directory}");
        }
    }

    public static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = mode, Cache = SqliteCacheMode.Private, Pooling = false, DefaultTimeout = 30
        }.ConnectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static SortedDictionary<string, long> ReadTableCounts(SqliteConnection database)
    {
        var names = new List<string>();
        using (var schema = database.CreateCommand())
        {
            schema.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var reader = schema.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
        }
        var counts = new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            using var count = database.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM \"{name.Replace("\"", "\"\"")}\";";
            counts.Add(name, (long)count.ExecuteScalar()!);
        }
        return counts;
    }

    private static bool IsInside(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
