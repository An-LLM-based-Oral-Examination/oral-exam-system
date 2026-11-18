using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.AcademicHierarchy;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.UnitTests.TestSupport;
using Xunit;

namespace OralExamination.UnitTests.Features.AcademicHierarchy;

public sealed class StudentCourseAccessTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("dropped")]
    [InlineData("inactive_class")]
    [InlineData("disabled_offering")]
    public async Task PracticeAndMockStartAndQuota_RejectStudentOutsideActiveClass(string state)
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new OralExamDbContext(options);
        var course = new Course { Code = "TEST101", Name = "Kiểm thử quyền lớp", IsActive = true };
        var student = new User { Email = "access-test@fpt.edu.vn", FullName = "Sinh viên", Role = "student", IsActive = true };
        context.Courses.Add(course);
        context.Users.Add(student);
        await context.SaveChangesAsync();

        if (state != "missing")
        {
            await StudentEnrollmentFixture.EnrollAsync(context, course.Id, student.Id);
            var enrollment = await context.ClassEnrollments.SingleAsync();
            if (state == "dropped")
            {
                enrollment.Status = "dropped";
            }
            else if (state == "inactive_class")
            {
                enrollment.Class.IsActive = false;
            }
            else
            {
                var offering = new CourseOffering
                {
                    CourseId = course.Id,
                    SemesterId = enrollment.Class.SemesterId,
                    IsEnabled = false
                };
                context.CourseOfferings.Add(offering);
                enrollment.Class.CourseOfferingId = offering.Id;
            }
            await context.SaveChangesAsync();
        }

        (await StudentCourseAccess.IsEnrolledAsync(context, student.Id, course.Id, CancellationToken.None))
            .Should().BeFalse();

        var practice = await new StartPracticeSessionCommandHandler(context)
            .Handle(new StartPracticeSessionCommand(student.Id, course.Id, "easy", 1, false), CancellationToken.None);
        var mock = await new StartMockExamCommandHandler(context)
            .Handle(new StartMockExamCommand(course.Id, student.Id, false), CancellationToken.None);
        var quota = await new GetMockExamQuotaQueryHandler(context)
            .Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

        practice.IsSuccess.Should().BeFalse();
        practice.Error.Should().StartWith("FORBIDDEN:");
        mock.IsSuccess.Should().BeFalse();
        mock.Error.Should().StartWith("FORBIDDEN:");
        quota.IsSuccess.Should().BeFalse();
        quota.Error.Should().StartWith("FORBIDDEN:");
        (await context.PracticeSessions.CountAsync()).Should().Be(0);
        (await context.MockExamSessions.CountAsync()).Should().Be(0);
        (await context.MockExamQuotas.CountAsync()).Should().Be(0);
    }
}
