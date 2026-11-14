using System.Diagnostics.CodeAnalysis;
using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.InClassShifts;

internal static class InClassShiftAcademicContext
{
    public static bool IsOperational([NotNullWhen(true)] Class? classEntity) =>
        classEntity is { IsActive: true } &&
        classEntity.Course is { IsActive: true } &&
        (classEntity.CourseOffering is null || classEntity.CourseOffering.IsEnabled);
}
