using Wedding.LocalDataBackup;

try
{
    string? source = null, output = null;
    var includeUploads = false;
    var extraRoots = new List<UploadBackupRoot>();
    for (var i = 0; i < args.Length; i++)
    {
        string Value() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing argument value.");
        switch (args[i])
        {
            case "--source": source = Value(); break;
            case "--output": output = Value(); break;
            case "--include-uploads": includeUploads = true; break;
            case "--upload-root":
                var specification = Value();
                var separator = specification.IndexOf('=');
                if (separator <= 0) throw new ArgumentException("--upload-root requires archive/folder=absolute/source/folder.");
                extraRoots.Add(new(specification[(separator + 1)..], specification[..separator]));
                break;
            case "--help":
                Console.WriteLine("Run from the repository root. Options: --source PATH --output DIRECTORY --include-uploads --upload-root archive/folder=source/folder (repeatable). Defaults: src/Wedding.WebApi/WeddingPlatformDb.db and .deployment/backups. Source is always read-only; no SQL import or API restart.");
                return 0;
            default: throw new ArgumentException("Unknown option. Use --help.");
        }
    }

    source = Path.GetFullPath(source ?? Path.Combine("src", "Wedding.WebApi", "WeddingPlatformDb.db"));
    output = Path.GetFullPath(output ?? Path.Combine(".deployment", "backups"));
    if (includeUploads)
    {
        var webRoot = Path.GetDirectoryName(source)!;
        extraRoots.Add(new(Path.Combine(webRoot, "App_Data", "invitation-uploads"), "App_Data/invitation-uploads"));
        extraRoots.Add(new(Path.Combine(webRoot, "App_Data", "listing-uploads"), "App_Data/listing-uploads"));
        extraRoots.Add(new(Path.Combine(webRoot, "wwwroot", "uploads", "listings"), "wwwroot/uploads/listings"));
    }
    if (extraRoots.Count > 0)
        Console.WriteLine("Note: uploads are archived while readable. Concurrent uploads are not transactionally synchronized with the database snapshot; repeat the backup after uploads are quiet before a final cutover.");
    var backup = SqlitePreservationBackup.Create(new(source, output, extraRoots));
    Console.WriteLine("PASS: SQLite online backup, read-only source, quick_check=ok. No migration, SQL database creation or API restart performed.");
    Console.WriteLine($"Backup directory: {backup.BackupDirectory}");
    Console.WriteLine($"Database SHA256: {backup.DatabaseSha256}");
    foreach (var table in backup.TableCounts) Console.WriteLine($"{table.Key}: {table.Value}");
    if (backup.UploadArchivePath != null)
    {
        Console.WriteLine($"Uploads archived: {backup.UploadFileCount} files");
        Console.WriteLine($"Uploads SHA256: {backup.UploadArchiveSha256}");
    }
    Console.WriteLine("Private backup contains user information. Keep it outside hosting/public folders and out of Git.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Backup failed ({error.GetType().Name}). {error.Message}");
    return 1;
}
