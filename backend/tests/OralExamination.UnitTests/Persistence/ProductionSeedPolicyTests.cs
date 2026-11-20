using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OralExamination.API.Configuration;
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
}
