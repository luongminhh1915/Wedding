using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

// One-off, explicitly requested LOCAL provisioning, never part of startup or publish.
// No reset, seed, promotion, password replacement, schema migration or external delivery.
if (args.Length != 0 || !OperatingSystem.IsWindows()) throw new InvalidOperationException("Local Windows provisioning accepts no arguments.");
const string database = "WeddingMatch_Dev";
const string connection = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true";
// Fixed, independently verified local workspace; never accept an arbitrary database target.
var repository = @"D:\Gitgithubhub\Wedding";
var privateDirectory = Path.Combine(repository, ".deployment", "private", "staff-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(privateDirectory);
var acl = new DirectorySecurity();
acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Missing Windows identity.");
foreach (var identity in new[] { sid, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
    acl.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
new DirectoryInfo(privateDirectory).SetAccessControl(acl);
var credentialsPath = Path.Combine(privateDirectory, "login-accounts.json");
var specs = new[] {
    (Name: "Quản trị Wedding Match", Email: "admin@wedding.com", Phone: "0000000001", Role: UserRole.SuperAdmin),
    (Name: "Tài chính Wedding Match", Email: "finance@wedding.com", Phone: "0000000002", Role: UserRole.Finance),
    (Name: "Kiểm duyệt Wedding Match", Email: "moderator@wedding.com", Phone: "0000000003", Role: UserRole.Moderator)
};
var hasher = new PasswordHasher();
var credentials = specs.Select(s => new Credential(s.Email, s.Role.ToString(), "WM!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)))).ToArray();
var users = specs.Select((s, i) => User.Create(s.Name, s.Email, s.Phone, hasher.Hash(credentials[i].Password), s.Role)).ToArray();
var emails = specs.Select(s => s.Email).ToList();
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
await db.Database.OpenConnectionAsync();
if (db.Database.GetDbConnection().Database != database) throw new InvalidOperationException("Unexpected database.");
await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
if (await db.Users.AnyAsync(u => emails.Contains(u.Email) || u.Role == UserRole.SuperAdmin))
    throw new InvalidOperationException("An administrator or requested email already exists. Nothing is changed; inspect before retrying.");
var originalIds = await db.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
db.Users.AddRange(users);
foreach (var u in users)
    db.AuditLogs.Add(AuditLog.Create(u.Role == UserRole.SuperAdmin ? null : users[0].Id,
        u.Role == UserRole.SuperAdmin ? "UserRequestedLocalProvisioning" : "SuperAdmin", "LocalStaffProvisioning.Create", "User", u.Id.ToString(),
        JsonSerializer.Serialize(new { role = u.Role.ToString(), requestedByUser = true, phone = "Unassigned placeholder; not OTP verified" })));
await db.SaveChangesAsync();
// Write before commit so a file error cannot leave accounts with lost credentials.
await using (var output = new FileStream(credentialsPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    await JsonSerializer.SerializeAsync(output, new { Database = database, Accounts = credentials,
        Note = "Local study accounts requested by the user. Change passwords after first login. Phone values are unassigned placeholders, not verified numbers." },
        new JsonSerializerOptions { WriteIndented = true });
await tx.CommitAsync();
db.ChangeTracker.Clear();
var created = await db.Users.AsNoTracking().Where(u => emails.Contains(u.Email)).ToListAsync();
if (created.Count != 3 || !created.All(u => u.IsActive && credentials.Any(c => c.Email == u.Email && c.Role == u.Role.ToString() && hasher.Verify(c.Password, u.PasswordHash))))
    throw new InvalidOperationException("Verification failed; inspect newly created accounts without resetting.");
if (await db.Users.CountAsync() != originalIds.Count + 3 || await db.Users.CountAsync(u => originalIds.Contains(u.Id)) != originalIds.Count)
    throw new InvalidOperationException("Unexpected account count; inspect without resetting.");
Console.WriteLine(JsonSerializer.Serialize(new { Created = created.Select(u => new { u.Email, Role = u.Role.ToString() }),
    Database = database, CredentialsPath = credentialsPath, OriginalAccountsPreserved = originalIds.Count }));

sealed record Credential(string Email, string Role, string Password)
{ public override string ToString() => "Credential { [REDACTED] }"; }
