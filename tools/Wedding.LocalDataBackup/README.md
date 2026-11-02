# Preserve an existing local SQLite database (no migration)

This offline preservation tool opens the source with SQLite **ReadOnly** mode and uses
`SqliteConnection.BackupDatabase`. It includes committed data still in `-wal`; copying the
`.db` file alone is not safe while the application is running. It does not use the current
EF model, change the source schema, open SQL Server, seed accounts, import records, stop,
or restart the API.

Run from the repository root with .NET 10 SDK:

```powershell
dotnet run --project tools/Wedding.LocalDataBackup --artifacts-path .deployment/backup-tool-build -- --include-uploads
```

Defaults are `src/Wedding.WebApi/WeddingPlatformDb.db` and `.deployment/backups/`. An explicit
source/output and extra upload roots can be supplied:

```powershell
dotnet run --project tools/Wedding.LocalDataBackup --artifacts-path .deployment/backup-tool-build -- --source D:/data/old.db --output D:/private-backups --upload-root App_Data/invitation-uploads=D:/data/invitation-uploads
```

`--include-uploads` opts into existing folders relative to the source database's directory:

- `App_Data/invitation-uploads`
- `App_Data/listing-uploads`
- `wwwroot/uploads/listings` (legacy listing files)

These exact relative folder names are retained inside `local-uploads.zip`. Missing folders
are skipped. Symlink/junction roots are rejected and nested reparse points are not followed.
File bytes can change during a live upload; this archive is not transactionally synchronized
with the database snapshot. Repeat once uploads are quiet before a final cutover. This tool
does not stop the application for you.

Each run creates a new timestamp/GUID directory and never overwrites another backup:

```text
sqlite-<UTC timestamp>-<GUID>/
  WeddingPlatformDb.snapshot.db
  local-uploads.zip              # only when upload roots were requested
  manifest.json                 # written only after successful verification
```

Verification performs SQLite `PRAGMA quick_check`, counts every non-system table without
printing any records, and records SHA256 hashes for the self-contained database and optional
ZIP. A failure retains partial artifacts without a successful manifest. The manifest records
all table counts and source path; terminal output contains only paths, hashes and row counts,
never user names, email addresses, phone numbers, password hashes or tokens.

The ignored `.deployment/` folder is not an encryption mechanism. Backups contain personal
data: keep them on a private encrypted disk or an access-controlled encrypted backup location,
never in `wwwroot`, a deployment ZIP, public storage, Git or shared chat attachments. Copy a
successful backup to a second private location and verify its hash before removing anything.

Changing the API from SQLite to SQL Server does **not** import these rows automatically.
Keep the original source and this snapshot. Any future import needs explicit source/target
mapping and validation, separately from deployment/migrations. This tool deliberately never
deletes the original database or uploads.
