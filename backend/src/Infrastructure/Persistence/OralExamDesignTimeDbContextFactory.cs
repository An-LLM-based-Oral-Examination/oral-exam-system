using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace OralExamination.Infrastructure.Persistence;

public sealed class OralExamDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OralExamDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=oralexam_offline_design;Username=oralexam;Password=oralexam_password";

    public OralExamDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "src", "API");
        if (Directory.Exists(apiPath))
        {
            basePath = apiPath;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("OralExamDb")
            ?? FallbackConnectionString;

        var builder = new DbContextOptionsBuilder<OralExamDbContext>();
        builder.UseNpgsql(
            connectionString,
            npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

        return new OralExamDbContext(builder.Options);
    }
}
