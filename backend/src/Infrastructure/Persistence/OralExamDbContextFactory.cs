using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace OralExamination.Infrastructure.Persistence;

public class OralExamDbContextFactory : IDesignTimeDbContextFactory<OralExamDbContext>
{
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
            ?? "Host=localhost;Port=5432;Database=oralexam_db;Username=oralexam;Password=oralexam_password";

        var builder = new DbContextOptionsBuilder<OralExamDbContext>();
        builder.UseNpgsql(connectionString);

        return new OralExamDbContext(builder.Options);
    }
}
