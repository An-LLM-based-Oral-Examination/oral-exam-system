using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OralExamination.Infrastructure;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Infrastructure;

public sealed class DependencyInjectionPoolTests
{
    [Fact(DisplayName = "AZ-B08: AddInfrastructure giữ nguyên Maximum Pool Size=80 từ ConnectionString thay vì ép >= 200")]
    public void AddInfrastructure_PreservesConfiguredMaxPoolSize_Below200()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Database:AllowInMemoryFallback"] = "false",
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=secret_pwd;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=80"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var actualConnStr = context.Database.GetConnectionString();

        actualConnStr.Should().NotBeNullOrWhiteSpace();
        var builder = new NpgsqlConnectionStringBuilder(actualConnStr);
        builder.MaxPoolSize.Should().Be(80);
        builder.MinPoolSize.Should().Be(10);
    }

    [Fact(DisplayName = "AZ-B08: AddInfrastructure không vượt MaxPoolSize khi MaxPoolSize < 10")]
    public void AddInfrastructure_ClampsMinPoolSize_ToMaxPoolSize_WhenMaxPoolSizeBelowTen()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Database:AllowInMemoryFallback"] = "false",
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=secret_pwd;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var builder = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());

        builder.MaxPoolSize.Should().Be(5);
        builder.MinPoolSize.Should().Be(5);
    }

    [Fact(DisplayName = "AZ-B08: AddInfrastructure hạ MinPoolSize về MaxPoolSize khi MinPoolSize >= 10 vượt MaxPoolSize")]
    public void AddInfrastructure_ClampsMinPoolSize_ToMaxPoolSize_WhenMinExceedsMaxAndMinAtLeastTen()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Database:AllowInMemoryFallback"] = "false",
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=5432;Database=oralexam_db;Username=oralexam;Password=secret_pwd;Pooling=true;Minimum Pool Size=20;Maximum Pool Size=5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OralExamDbContext>();
        var builder = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());

        builder.MaxPoolSize.Should().Be(5);
        builder.MinPoolSize.Should().Be(5);
    }
}
