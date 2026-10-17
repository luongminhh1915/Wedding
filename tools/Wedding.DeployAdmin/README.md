# First production administrator

This interactive command creates the **first** SuperAdmin in an already-migrated SQL Server database. It is not part of API startup, demo seeding, public registration, or the deployed website. Keep this tool and its private configuration on the operator's machine.

1. Apply the reviewed SQL migration script to the intended hosting database, and confirm that the website has HTTPS before collecting user data.
2. Create `.deployment/private/wedding.production.json` using the deployment configuration helper. Keep this file out of Git, release ZIPs, browser-accessible folders, screenshots, and chat. It must contain explicit `Database:Provider=SqlServer` and `ConnectionStrings:DefaultConnection`; no inline secret argument is accepted.
3. Open an interactive terminal at the repository root:

```powershell
dotnet run --project tools/Wedding.DeployAdmin -- --help
dotnet run --project tools/Wedding.DeployAdmin -- --config .deployment/private/wedding.production.json
```

The command displays only the SQL host and database name, requires typing the exact database name, checks connectivity and pending migrations, then asks for name/email/phone and a masked password twice. The password must be at least 12 characters and at most 72 UTF-8 bytes. There is no default password, and credentials cannot be piped into the tool.

If **any** SuperAdmin already exists, even a locked one, the command refuses. If the email belongs to any existing user, the command refuses; it never upgrades a customer/vendor, resets a password, unlocks an account, migrates the schema, or deletes data. Creation and the minimal audit entry occur in one serializable transaction. If it fails or is interrupted during commit, check the database state before trying again. Sign in through the normal website after success, and create other staff roles from the administrator's account-management screen.

This command requires SQL connectivity from the operator's computer. If the host blocks remote access, use its documented remote-connection procedure; do not weaken TLS, expose the database publicly, add an unauthenticated bootstrap endpoint, or put this executable in the public website as a workaround.
