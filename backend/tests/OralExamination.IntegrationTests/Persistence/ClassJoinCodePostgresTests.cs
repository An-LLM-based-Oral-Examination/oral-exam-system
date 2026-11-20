using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OralExamination.Domain.Entities;
using OralExamination.IntegrationTests.Support;
using Xunit;

namespace OralExamination.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class ClassJoinCodePostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task TwoClasses_WithDefaultJoinCodes_PersistWithoutUniqueViolation()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var context = database.CreateContext();
        var academic = await TestData.AcademicAsync(context);
        var classes = Enumerable.Range(1, 2).Select(index => new Class
        {
            Code = $"IT-CLASS-{index}", Name = $"Integration class {index}",
            CourseId = academic.Course.Id, SemesterId = academic.Course.SemesterId,
            LecturerId = academic.Lecturer.Id
        }).ToArray();
        context.Classes.AddRange(classes);

        await context.SaveChangesAsync();

        await using var verify = database.CreateContext();
        var persisted = await verify.Classes.Where(item => item.CourseId == academic.Course.Id).ToListAsync();
        persisted.Should().HaveCount(2);
        persisted.Select(item => item.JoinCode).Should().OnlyHaveUniqueItems();
        persisted.Should().OnlyContain(item => item.JoinCode.Length == 16 && item.JoinCode.StartsWith("CLS"));

        // A generated default must not weaken the database's uniqueness guarantee.
        context.Classes.Add(new Class
        {
            Code = "IT-CLASS-DUPLICATE", Name = "Duplicate code",
            CourseId = academic.Course.Id, SemesterId = academic.Course.SemesterId,
            JoinCode = classes[0].JoinCode
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be("ix_classes_join_code");
    }
}
