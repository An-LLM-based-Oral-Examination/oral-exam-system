using OralExamination.Domain.Entities;

namespace OralExamination.Application.Features.OfficialExams;

/// <summary>
/// Ca thi nhận điểm danh mới khi trạng thái còn scheduled hoặc in_progress
/// và thời điểm gọi nằm trong [StartTime, EndTime), đồng thời cho phép khôi phục/nộp audio
/// trong khoảng ân hạn [StartTime, EndTime + SubmissionGracePeriod].
/// </summary>
public static class ExamShiftWindow
{
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public static readonly TimeSpan SubmissionGracePeriod = TimeSpan.FromMinutes(2);

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

    public static bool ContainsWithSubmissionGrace(RealExamSessionShift shift, DateTime atUtc)
    {
        var at = AsUtc(atUtc);
        var start = AsUtc(shift.StartTime);
        var endWithGrace = AsUtc(shift.EndTime).Add(SubmissionGracePeriod);
        return at >= start && at <= endWithGrace;
    }

    public static bool AcceptsCheckIn(RealExamSessionShift shift, DateTime atUtc)
    {
        return IsOpenStatus(shift.Status) && Contains(shift, atUtc);
    }

    public static string? ValidateActiveSubmissionWindow(RealExamSessionShift shift, DateTime utcNow)
    {
        if (!shift.IsExamPublished)
        {
            return "CONFLICT: Ca thi chưa được mở phát đề.";
        }

        if (string.Equals(shift.Status, Cancelled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(shift.Status, Completed, StringComparison.OrdinalIgnoreCase))
        {
            return "CONFLICT: Ca thi đã kết thúc hoặc bị hủy, không thể tiếp tục nộp bài.";
        }

        if (shift.EndTime != default && AsUtc(utcNow) > AsUtc(shift.EndTime).Add(SubmissionGracePeriod))
        {
            return "CONFLICT: Đã hết thời gian làm bài của ca thi (bao gồm 2 phút ân hạn).";
        }

        return null;
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
