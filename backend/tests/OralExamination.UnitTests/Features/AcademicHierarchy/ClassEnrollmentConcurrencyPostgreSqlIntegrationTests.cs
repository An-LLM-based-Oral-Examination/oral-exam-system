using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.UnitTests.Features.InClassShifts;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

[Trait("Category", "Integration")]
[Collection("PostgreSqlNonParallelSchemaCollection")]
public sealed class ClassEnrollmentConcurrencyPostgreSqlIntegrationTests
{
    private static OralExamDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseNpgsql(InClassShiftConcurrencyIntegrationTests.GetPostgresConnectionString())
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact]
    public async Task OneRemainingSeat_ConcurrentJoins_EnrollExactlyOneStudent()
    {
        var semesterId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..12];

        await using var seed = NewContext();
        (await seed.Database.CanConnectAsync()).Should().BeTrue("PostgreSQL integration database must be available");
        seed.Semesters.Add(new Semester
        {
            Id = semesterId, Code = $"CE{suffix}", Name = "Enrollment concurrency",
            StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31)
        });
        seed.Courses.Add(new Course
        {
            Id = courseId, Code = $"EC{suffix}", Name = "Enrollment concurrency", SemesterId = semesterId
        });
        seed.Classes.Add(new Class
        {
            Id = classId, Code = $"CL{suffix}", JoinCode = $"JC{suffix}", Name = "One seat",
            CourseId = courseId, SemesterId = semesterId, MaxStudents = 1,
            IsJoinEnabled = true, AllowSelfEnrollment = true, IsActive = true
        });
        seed.Users.AddRange(
            new User { Id = firstId, Email = $"ce1-{suffix}@example.test", FullName = "Student one", Role = "student" },
            new User { Id = secondId, Email = $"ce2-{suffix}@example.test", FullName = "Student two", Role = "student" });
        await seed.SaveChangesAsync();

        try
        {
            await using var holder = NewContext();
            await using var first = NewContext();
            await using var second = NewContext();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var held = holder.ExecuteClassEnrollmentAsync(classId, async ct =>
            {
                entered.SetResult();
                await release.Task.WaitAsync(ct);
                return true;
            });

            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var hasher = new Mock<IPasswordHasher>().Object;
                var validator = new JoinClassCommandValidator();
                var firstJoin = new JoinClassCommandHandler(first, hasher, validator)
                    .Handle(new JoinClassCommand(firstId, null, null, classId), CancellationToken.None);
                var secondJoin = new JoinClassCommandHandler(second, hasher, validator)
                    .Handle(new JoinClassCommand(secondId, null, null, classId), CancellationToken.None);

                await Task.Delay(200);
                firstJoin.IsCompleted.Should().BeFalse("the first join must wait on the PostgreSQL class lock");
                secondJoin.IsCompleted.Should().BeFalse("the second join must wait on the PostgreSQL class lock");
                release.SetResult();
                (await held).Should().BeTrue();

                var results = await Task.WhenAll(firstJoin, secondJoin).WaitAsync(TimeSpan.FromSeconds(15));
                results.Count(result => result.IsSuccess).Should().Be(1);
                results.Count(result => !result.IsSuccess && result.Error == "Lớp học đã đạt số lượng sinh viên tối đa.")
                    .Should().Be(1);
                await using var verify = NewContext();
                (await verify.ClassEnrollments.CountAsync(e => e.ClassId == classId && e.Status == "enrolled"))
                    .Should().Be(1);
            }
            finally
            {
                release.TrySetResult();
                await held;
            }
        }
        finally
        {
            await using var cleanup = NewContext();
            await cleanup.AuditLogs.Where(a => a.EntityId == classId).ExecuteDeleteAsync();
            await cleanup.ClassEnrollments.Where(e => e.ClassId == classId).ExecuteDeleteAsync();
            await cleanup.Classes.Where(c => c.Id == classId).ExecuteDeleteAsync();
            await cleanup.Courses.Where(c => c.Id == courseId).ExecuteDeleteAsync();
            await cleanup.Semesters.Where(s => s.Id == semesterId).ExecuteDeleteAsync();
            await cleanup.Users.Where(u => u.Id == firstId || u.Id == secondId).ExecuteDeleteAsync();
            (await cleanup.ClassEnrollments.CountAsync(e => e.ClassId == classId)).Should().Be(0);
        }
    }
}
