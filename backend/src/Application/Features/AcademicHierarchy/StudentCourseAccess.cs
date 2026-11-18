using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.Application.Features.AcademicHierarchy;

public static class StudentCourseAccess
{
    public const string Denied = "FORBIDDEN:Sinh viên phải đang tham gia một lớp hoạt động của môn học này.";

    public static Task<bool> IsEnrolledAsync(
        IApplicationDbContext context, Guid studentId, Guid courseId, CancellationToken cancellationToken) =>
        context.ClassEnrollments.AsNoTracking().AnyAsync(e =>
            e.StudentId == studentId &&
            e.Status == "enrolled" &&
            e.Class.CourseId == courseId &&
            e.Class.IsActive &&
            e.Class.Course.IsActive &&
            (e.Class.CourseOffering == null || e.Class.CourseOffering.IsEnabled), cancellationToken);
}
