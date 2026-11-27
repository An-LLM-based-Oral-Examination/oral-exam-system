using System;
using OralExamination.Application.Common.Authorization;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.AcademicHierarchy;

internal static class ClassRosterAccess
{
    public static bool CanManage(Class targetClass, Guid actorUserId, string? actorRole)
    {
        if (actorUserId == Guid.Empty)
        {
            return false;
        }

        if (targetClass.LecturerId.HasValue && targetClass.LecturerId.Value == actorUserId)
        {
            return true;
        }

        return targetClass.Course is not null
            && AcademicCourseOwnership.IsOwnedBy(targetClass.Course, actorUserId);
    }
}
