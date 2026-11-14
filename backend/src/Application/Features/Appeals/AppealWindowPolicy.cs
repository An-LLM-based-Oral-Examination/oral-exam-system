using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;

namespace OralExamination.Application.Features.Appeals;

public sealed class AppealWindowOptions
{
    public const int DefaultHours = 72;
    public int Hours { get; set; } = DefaultHours;
}

public static class AppealWindowPolicy
{
    public static bool IsPublished(StudentExamTicket ticket) =>
        ticket.Status.Equals(ExamTicketStatus.Published, StringComparison.OrdinalIgnoreCase)
        || ticket.Status.Equals(ExamTicketStatus.Locked, StringComparison.OrdinalIgnoreCase);

    public static string? Restriction(StudentExamTicket ticket, DateTime nowUtc, int hours)
    {
        if (!IsPublished(ticket)) return "Chỉ được phúc khảo sau khi điểm thi được công bố.";
        if (ticket.StudentAcknowledgementStatus.Equals("ACKNOWLEDGED", StringComparison.OrdinalIgnoreCase))
            return "Bạn đã xác nhận đồng ý điểm nên không thể gửi phúc khảo.";
        if (!ticket.GradesPublishedAt.HasValue) return "Chưa xác định thời điểm công bố điểm. Vui lòng liên hệ Khảo thí.";
        if (nowUtc >= ticket.GradesPublishedAt.Value.AddHours(hours)) return "Đã hết thời hạn phúc khảo.";
        return null;
    }
}
