using System.Linq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.QuestionBank;

internal static class QuestionCourseAccess
{
    public static IQueryable<Guid> AllowedCourseIds(IApplicationDbContext context, string? role, Guid actorId)
    {
        if (actorId == Guid.Empty)
            return context.Courses.Where(_ => false).Select(c => c.Id);

        if (string.Equals(role, UserRole.DepartmentHead, StringComparison.OrdinalIgnoreCase))
            return context.Courses.Where(c =>
                c.AcademicProgram != null && c.AcademicProgram.DepartmentHeadUserId == actorId ||
                (c.AcademicProgram == null || c.AcademicProgram.DepartmentHeadUserId == null || c.AcademicProgram.DepartmentHeadUserId == Guid.Empty)
                && c.DepartmentHeadUserId == actorId)
                .Select(c => c.Id);

        if (string.Equals(role, UserRole.Lecturer, StringComparison.OrdinalIgnoreCase))
            return context.CourseLecturers.Where(cl => cl.LecturerUserId == actorId && cl.IsActive)
                .Select(cl => cl.CourseId)
                .Union(context.Classes.Where(c => c.LecturerId == actorId && c.IsActive)
                    .Select(c => c.CourseId));

        return context.Courses.Where(_ => false).Select(c => c.Id);
    }
}
