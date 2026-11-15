using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Student.DTOs;
using OralExamination.Domain.Enums;
using OralExamination.Application.Features.Appeals;

namespace OralExamination.Application.Features.Student.Commands.AcknowledgeOfficialGrade;

/// <summary>
/// Handler xử lý sinh viên xác nhận đồng ý kết quả điểm thi thật (ACKNOWLEDGED) trên Student Portal (MF-04).
/// Chỉ cho phép xác nhận khi vé thi thuộc về chính sinh viên đó và điểm ca thi đã được công bố (PUBLISHED/LOCKED).
/// </summary>
public sealed class AcknowledgeOfficialGradeCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AcknowledgeOfficialGradeCommand, Result<AcknowledgeOfficialGradeResponseDto>>
{
    public async Task<Result<AcknowledgeOfficialGradeResponseDto>> Handle(
        AcknowledgeOfficialGradeCommand request,
        CancellationToken cancellationToken)
        => await context.ExecuteTicketDecisionAsync(request.TicketId, ct => HandleLocked(request, ct), cancellationToken);

    private async Task<Result<AcknowledgeOfficialGradeResponseDto>> HandleLocked(
        AcknowledgeOfficialGradeCommand request, CancellationToken cancellationToken)
    {
        var ticket = await context.StudentExamTickets
            .FirstOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result<AcknowledgeOfficialGradeResponseDto>.Failure("Vé thi không tồn tại trong hệ thống.");
        }

        if (ticket.StudentId != request.StudentId)
        {
            return Result<AcknowledgeOfficialGradeResponseDto>.Failure("Bạn không có quyền xác nhận điểm cho vé thi của sinh viên khác.");
        }

        bool isPublishedOrLocked = AppealWindowPolicy.IsPublished(ticket) && ticket.GradesPublishedAt.HasValue;

        if (!isPublishedOrLocked)
        {
            return Result<AcknowledgeOfficialGradeResponseDto>.Failure("Chỉ có thể xác nhận điểm sau khi giảng viên đã công bố điểm ca thi.");
        }

        var nowUtc = DateTime.UtcNow;
        if (await context.AppealRequests.AnyAsync(a => a.TicketId == ticket.Id &&
            (a.Status == AppealStatus.SubmittedToExamOffice ||
             a.Status == AppealStatus.AssignedToLecturer ||
             a.Status == AppealStatus.RegradedByLecturer), cancellationToken))
            return Result<AcknowledgeOfficialGradeResponseDto>.Failure("CONFLICT: Đang có đơn phúc khảo xử lý nên không thể xác nhận đồng ý điểm.");
        if (!string.Equals(ticket.StudentAcknowledgementStatus, "ACKNOWLEDGED", StringComparison.OrdinalIgnoreCase))
        {
            ticket.StudentAcknowledgementStatus = "ACKNOWLEDGED";
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result<AcknowledgeOfficialGradeResponseDto>.Success(new AcknowledgeOfficialGradeResponseDto
        {
            TicketId = ticket.Id,
            StudentId = ticket.StudentId,
            StudentAcknowledgementStatus = ticket.StudentAcknowledgementStatus,
            AcknowledgedAtUtc = nowUtc,
            Message = "Đã ghi nhận xác nhận đồng ý kết quả điểm thi chính thức thành công."
        });
    }
}
