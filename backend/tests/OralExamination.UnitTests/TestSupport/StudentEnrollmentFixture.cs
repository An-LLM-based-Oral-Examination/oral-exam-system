using System;
using System.Threading.Tasks;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;

namespace OralExamination.UnitTests.TestSupport;

internal static class StudentEnrollmentFixture
{
    public static async Task EnrollAsync(OralExamDbContext context, Guid courseId, Guid studentId)
    {
        var semester = new Semester
        {
            Code = $"TEST-{Guid.NewGuid():N}",
            Name = "Học kỳ kiểm thử",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        var courseClass = new Class
        {
            Code = $"TEST-{Guid.NewGuid():N}",
            Name = "Lớp kiểm thử",
            CourseId = courseId,
            SemesterId = semester.Id,
            IsActive = true
        };
        context.Semesters.Add(semester);
        context.Classes.Add(courseClass);
        context.ClassEnrollments.Add(new ClassEnrollment
        {
            ClassId = courseClass.Id,
            StudentId = studentId,
            Status = "enrolled"
        });
        await context.SaveChangesAsync();
    }
}
