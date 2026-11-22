using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Persistence;

public sealed class ProductionSeedPolicyTests
{
    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Development", null, false)]
    [InlineData("Development", "true", true)]
    [InlineData("Production", "false", false)]
    public void DevelopmentSeed_IsExplicitOptInOnly(string environment, string? value, bool expected)
    {
        var configuration = Config(value);

        DatabaseSeedPolicy.IsDevelopmentSeedEnabled(configuration, environment).Should().Be(expected);
    }

    [Theory]
    [InlineData("Production", "true")]
    [InlineData("Staging", "true")]
    [InlineData("Production", "invalid")]
    [InlineData("Development", "invalid")]
    public void UnsafeOrMalformedConfiguration_FailsClosed(string environment, string value)
    {
        var act = () => DatabaseSeedPolicy.IsDevelopmentSeedEnabled(Config(value), environment);

        act.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration Config(string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["DATABASE_ENABLE_DEVELOPMENT_SEED"] = value }).Build();

    [Theory]
    [InlineData("Database:EnableTask04ReviewSeed")]
    [InlineData("DATABASE_ENABLE_TASK04_REVIEW_SEED")]
    public void ReviewSeed_CannotRunInProduction(string key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [key] = "true" }).Build();

        var act = () => DatabaseSeedPolicy.IsDevelopmentSeedEnabled(configuration, "Production");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Database:EnableDevelopmentSeed", "DATABASE_ENABLE_DEVELOPMENT_SEED")]
    [InlineData("Database:EnableTask04ReviewSeed", "DATABASE_ENABLE_TASK04_REVIEW_SEED")]
    public void ConflictingAliases_CannotHideEnabledSeedInProduction(string canonical, string alias)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [canonical] = "false", [alias] = "true" }).Build();

        var act = () => DatabaseSeedPolicy.IsDevelopmentSeedEnabled(configuration, "Production");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AcademicCourseBankImport_DefaultsToTrue_AndAllowsExplicitFalse()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        DatabaseSeedPolicy.IsAcademicCourseBankImportEnabled(emptyConfig).Should().BeTrue();

        var disabledConfig = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DATABASE_ENABLE_ACADEMIC_IMPORT"] = "false" }).Build();
        DatabaseSeedPolicy.IsAcademicCourseBankImportEnabled(disabledConfig).Should().BeFalse();
    }

    [Fact]
    public void AcademicCourseBankImport_MalformedOrConflictingFlags_FailClosed()
    {
        var invalidConfig = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DATABASE_ENABLE_ACADEMIC_IMPORT"] = "maybe" }).Build();
        var actInvalid = () => DatabaseSeedPolicy.IsAcademicCourseBankImportEnabled(invalidConfig);
        actInvalid.Should().Throw<InvalidOperationException>();

        var conflictingConfig = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:EnableAcademicCourseBankImport"] = "true",
                ["DATABASE_ENABLE_ACADEMIC_IMPORT"] = "false"
            }).Build();
        var actConflict = () => DatabaseSeedPolicy.IsAcademicCourseBankImportEnabled(conflictingConfig);
        actConflict.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CliCommandOptions_ParsesAndStripsRecognizedSwitches()
    {
        var importOk = CliCommandOptions.TryParse(
            ["--import-course-banks", "--environment=Production"],
            out var importOpts,
            out var importErr);
        importOk.Should().BeTrue();
        importErr.Should().BeNull();
        importOpts.Mode.Should().Be(CliMode.ImportCourseBanks);
        importOpts.RemainingArgs.Should().Equal("--environment=Production");

        var provOk = CliCommandOptions.TryParse(
            ["--provision-test-accounts", "--config", "/tmp/accounts.json"],
            out var provOpts,
            out var provErr);
        provOk.Should().BeTrue();
        provErr.Should().BeNull();
        provOpts.Mode.Should().Be(CliMode.ProvisionTestAccounts);
        provOpts.ConfigPath.Should().Be("/tmp/accounts.json");
        provOpts.RemainingArgs.Should().BeEmpty();

        var stdinOk = CliCommandOptions.TryParse(
            ["--provision-test-accounts", "--config", "-"],
            out var stdinOpts,
            out var stdinErr);
        stdinOk.Should().BeTrue();
        stdinErr.Should().BeNull();
        stdinOpts.ConfigPath.Should().Be("-");
    }

    [Theory]
    [InlineData("--import-course-banks", "--provision-test-accounts")]
    [InlineData("--provision-test-accounts", "--config")]
    [InlineData("--config", "/tmp/x.json")]
    [InlineData("--import-course-banks", "--unknown-flag")]
    [InlineData("--import-course-banks", "-v")]
    [InlineData("--import-course-banks", "-foo")]
    [InlineData("--provision-test-accounts", "-e", "Production")]
    public void CliCommandOptions_InvalidCombinations_FailClosed(params string[] args)
    {
        var ok = CliCommandOptions.TryParse(args, out _, out var err);
        ok.Should().BeFalse();
        err.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CliMode_ImportCourseBanks_BuildsCleanlyInDevelopmentWithoutJwtKey_ValidateOnBuild()
    {
        CliCommandOptions.TryParse(["--import-course-banks"], out var cliOptions, out var parseError)
            .Should().BeTrue(parseError);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=59999;Database=x;Username=x;Password=x",
            ["DATABASE_ALLOW_IN_MEMORY_FALLBACK"] = "true",
            ["DATABASE_ENABLE_DEVELOPMENT_SEED"] = "false",
            ["JWT_SIGNING_KEY"] = ""
        });
        builder.Environment.IsDevelopment().Should().BeTrue();

        Program.ConfigureServices(builder, cliOptions);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OralExamDbContext>().Should().NotBeNull();
        app.Services.GetRequiredService<DatabaseStartupState>().Should().NotBeNull();
        app.Services.GetServices<IHostedService>()
            .Should().NotContain(s => s.GetType().Assembly == typeof(Program).Assembly);
        scope.ServiceProvider.GetService<IMediator>().Should().BeNull();
    }

    [Fact]
    public async Task CliMode_ProvisionTestAccounts_BuildsCleanlyInDevelopment_ValidateOnBuild()
    {
        CliCommandOptions.TryParse(["--provision-test-accounts", "--config", "-"], out var cliOptions, out var parseError)
            .Should().BeTrue(parseError);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=59999;Database=x;Username=x;Password=x",
            ["DATABASE_ALLOW_IN_MEMORY_FALLBACK"] = "true",
            ["DATABASE_ENABLE_DEVELOPMENT_SEED"] = "false",
            ["JWT_SIGNING_KEY"] = "unit-test-jwt-signing-key-at-least-32-bytes-long"
        });
        builder.Environment.IsDevelopment().Should().BeTrue();

        Program.ConfigureServices(builder, cliOptions);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OralExamDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IClassJoinPasswordProtector>().Should().NotBeNull();
        app.Services.GetServices<IHostedService>()
            .Should().NotContain(s => s.GetType().Assembly == typeof(Program).Assembly);
        scope.ServiceProvider.GetService<IMediator>().Should().BeNull();
    }
}
