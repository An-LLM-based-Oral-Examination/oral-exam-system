using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams;

/// <summary>
/// Ca thi nhận điểm danh khi trạng thái còn scheduled hoặc in_progress
/// và thời điểm gọi nằm trong [StartTime, EndTime).
/// </summary>
public static class ExamShiftWindow
{
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static bool IsOpenStatus(string? status)
    {
        return string.Equals(status, Scheduled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, InProgress, StringComparison.OrdinalIgnoreCase);
    }

    public static bool Contains(RealExamSessionShift shift, DateTime atUtc)
    {
        var at = AsUtc(atUtc);
        var start = AsUtc(shift.StartTime);
        var end = AsUtc(shift.EndTime);
        return at >= start && at < end;
    }

    public static bool AcceptsCheckIn(RealExamSessionShift shift, DateTime atUtc)
    {
        return IsOpenStatus(shift.Status) && Contains(shift, atUtc);
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
