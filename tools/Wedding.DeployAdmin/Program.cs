using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Wedding Match: create the FIRST SQL Server SuperAdmin only.");
    Console.WriteLine("Usage: dotnet run --project tools/Wedding.DeployAdmin -- [--config <private-json-file>]");
    Console.WriteLine("Default config: .deployment/private/wedding.production.json (relative to current directory).");
    Console.WriteLine("Apply reviewed migrations separately first. This command never migrates, seeds demo data, promotes, or resets accounts.");
    Console.WriteLine("Passwords must be entered interactively; do not put secrets into command arguments or chat.");
    return 0;
}

if (args.Length != 0 && (args.Length != 2 || args[0] != "--config" || string.IsNullOrWhiteSpace(args[1])))
{
    Console.Error.WriteLine("Invalid arguments. Use --help. Only a private configuration file path is accepted; never pass a connection string or password inline.");
    return 2;
}
if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.Error.WriteLine("Refusing redirected input/output. Run this tool in an interactive terminal so passwords are masked and the target can be confirmed.");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var path = Path.GetFullPath(args.Length == 2 ? args[1] : Path.Combine(".deployment", "private", "wedding.production.json"));
    if (!File.Exists(path)) throw new DeploymentInputException("Private configuration file not found. Create it outside the deploy ZIP; use --config to select its path.");
    var config = new ConfigurationBuilder().AddJsonFile(path, optional: false, reloadOnChange: false).Build();
    if (!string.Equals(config["Database:Provider"], "SqlServer", StringComparison.OrdinalIgnoreCase))
        throw new DeploymentInputException("Only an explicit Database:Provider=SqlServer target is permitted.");
    var connectionString = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString)) throw new DeploymentInputException("DefaultConnection is missing in the private configuration file.");
    var connection = new SqlConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog)
        || new[] { "master", "model", "msdb", "tempdb" }.Contains(connection.InitialCatalog.Trim(), StringComparer.OrdinalIgnoreCase))
        throw new DeploymentInputException("Specify a named application database, never a SQL Server system database.");
    if (!string.IsNullOrWhiteSpace(connection.AttachDBFilename)) throw new DeploymentInputException("Attached database files are not supported. Use a named SQL Server database.");
    Console.WriteLine($"SQL Server: {SafeLabel(connection.DataSource)}");
    Console.WriteLine($"Database:   {SafeLabel(connection.InitialCatalog)}");
    Console.WriteLine("This creates exactly one new SuperAdmin. Existing users are never changed.");
    Console.Write("Type the exact database name to continue: ");
    if (!string.Equals(Console.ReadLine(), connection.InitialCatalog, StringComparison.Ordinal))
        throw new OperationCanceledException("Target database was not confirmed.");

    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
    await using var db = new ApplicationDbContext(options);
    if (!await db.Database.CanConnectAsync(cancellation.Token)) throw new DeploymentInputException("Cannot connect to the selected SQL Server database. No account was created.");
    if ((await db.Database.GetPendingMigrationsAsync(cancellation.Token)).Any())
        throw new DeploymentInputException("Database migrations are pending. Apply the reviewed SQL script separately; this tool never changes the schema.");
    if (await db.Users.AnyAsync(user => user.Role == Wedding.Domain.Enums.UserRole.SuperAdmin, cancellation.Token))
        throw new DeploymentInputException("A SuperAdmin already exists, including locked accounts. No account was created or changed.");

    Console.Write("Full name: "); var fullName = Console.ReadLine() ?? "";
    Console.Write("Email: "); var email = Console.ReadLine() ?? "";
    Console.Write("Vietnamese phone number (10 digits starting with 0): "); var phone = Console.ReadLine() ?? "";
    var password = ReadMasked("Password (12+ characters, <=72 UTF-8 bytes): ", cancellation.Token);
    var confirmation = ReadMasked("Confirm password: ", cancellation.Token);
    var id = await new FirstAdministratorBootstrapper(db, new PasswordHasher())
        .CreateAsync(new(fullName, email, phone, password, confirmation), cancellation.Token);
    Console.WriteLine($"Created the first SuperAdmin. User ID: {id:D}. Sign in through the normal website; this tool issued no token.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Cancelled. No further action will be performed. Verify database state before retrying if cancellation happened during commit.");
    return 2;
}
catch (DomainException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
catch (DeploymentInputException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
catch (Exception)
{
    Console.Error.WriteLine("The operation failed. Check configuration, database schema, and connectivity locally. No credentials or provider diagnostics are printed.");
    return 1;
}

static string ReadMasked(string prompt, CancellationToken cancellation)
{
    Console.Write(prompt);
    var value = new System.Text.StringBuilder();
    while (true)
    {
        cancellation.ThrowIfCancellationRequested();
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException();
        if (key.Key == ConsoleKey.Backspace)
        {
            if (value.Length > 0) { value.Length--; Console.Write("\b \b"); }
            continue;
        }
        if (!char.IsControl(key.KeyChar) && value.Length < 1000) { value.Append(key.KeyChar); Console.Write('*'); }
    }
}

static string SafeLabel(string text) => new(text.Where(character => !char.IsControl(character)).ToArray());

sealed class DeploymentInputException(string message) : Exception(message);
