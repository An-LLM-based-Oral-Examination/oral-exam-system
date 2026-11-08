using System;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.AcademicHierarchy;

internal static class ClassRosterAccess
{
    public static bool CanManage(Class targetClass, Guid actorUserId, string? actorRole)
    {
        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        if (string.Equals(actorRole, UserRole.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (targetClass.LecturerId.HasValue && targetClass.LecturerId.Value == actorUserId)
        {
            return true;
        }

        var departmentHeadId = targetClass.Course?.AcademicProgram?.DepartmentHeadUserId
                               ?? targetClass.Course?.DepartmentHeadUserId;
        return departmentHeadId.HasValue && departmentHeadId.Value == actorUserId;
    }
}
