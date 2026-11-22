using System;
using Microsoft.EntityFrameworkCore;

namespace OralExamination.Application.Features.AcademicCatalog.Commands;

internal static class AcademicProgramConflictDetector
{
    public static bool IsDuplicateCodeViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            var type = current.GetType();
            var sqlState = type.GetProperty("SqlState")?.GetValue(current) as string;
            if (string.Equals(sqlState, "23505", StringComparison.Ordinal))
            {
                var constraintName = type.GetProperty("ConstraintName")?.GetValue(current) as string;
                return string.IsNullOrWhiteSpace(constraintName) ||
                       constraintName.Contains("academic_programs", StringComparison.OrdinalIgnoreCase);
            }

            if (current.Message.Contains("ix_academic_programs_code", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return exception.Message.Contains("ix_academic_programs_code", StringComparison.OrdinalIgnoreCase);
    }
}
