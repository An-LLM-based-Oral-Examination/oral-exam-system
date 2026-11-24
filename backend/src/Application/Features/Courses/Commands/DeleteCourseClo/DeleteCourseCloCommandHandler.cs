using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Authorization;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;

public sealed class DeleteCourseCloCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteCourseCloCommand, Result>
{
    public async Task<Result> Handle(DeleteCourseCloCommand request, CancellationToken cancellationToken)
    {
        var (role, actorId) = AcademicCourseOwnership.ParseActor(request.ActorRole);
        var course = await context.Courses.AsNoTracking().Include(c => c.AcademicProgram)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null) return Result.Failure("NOT_FOUND: Môn học không tồn tại.");
        if (!AcademicCourseOwnership.CanManage(course, role, actorId))
            return Result.Failure("FORBIDDEN: Chỉ Trưởng bộ môn phụ trách môn học hoặc Admin được quản lý CLO.");
        var clo = await context.CourseClos.FirstOrDefaultAsync(c => c.Id == request.CloId && c.CourseId == request.CourseId, cancellationToken);
        if (clo is null) return Result.Failure("NOT_FOUND: Chuẩn đầu ra (CLO) không tồn tại trong môn học này.");

        if (await context.PracticeQuestions.AsNoTracking().AnyAsync(q => q.CloId == clo.Id, cancellationToken)
            || await context.ExamQuestions.AsNoTracking().AnyAsync(q => q.CloId == clo.Id, cancellationToken)
            || await CourseCloDependencies.HasSlotReferenceAsync(context, clo, cancellationToken)
            || await CourseCloDependencies.HasInClassShiftReferenceAsync(context, clo, cancellationToken))
            return Result.Failure("CONFLICT: CLO_IN_USE: CLO đang được câu hỏi, cấu trúc đề hoặc ca thi trên lớp sử dụng; không thể xóa.");

        context.CourseClos.Remove(clo);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
