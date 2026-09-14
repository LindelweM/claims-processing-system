using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Claims.Persistence;

/// <summary>
/// Builds a context for the EF Core command-line tools, which run without the application host.
/// </summary>
/// <remarks>
/// Only used by commands such as <c>dotnet ef migrations add</c>. Set the connection string in
/// the <c>ClaimsDbConnection</c> environment variable; the fallback points at a local SQL Server
/// and is never used at runtime.
/// </remarks>
public sealed class DesignTimeContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    private const string ConnectionStringVariable = "ClaimsDbConnection";

    private const string LocalFallback =
        "Server=localhost,1433;Database=Claims;User Id=sa;Password=Your_password123;TrustServerCertificate=True";

    /// <inheritdoc />
    public ClaimsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? LocalFallback;

        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ClaimsDbContext(options);
    }
}
