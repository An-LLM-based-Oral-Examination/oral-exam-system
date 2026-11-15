namespace OralExamination.Application.Common.Models;

/// <summary>Stable conflict codes and actionable messages for shift management.</summary>
public static class ShiftMutationErrors
{
    public const string AlreadyStarted = "Ca thi đã đến giờ bắt đầu. Không thể sửa hoặc xóa; hãy giữ ca để bảo toàn lịch sử thi.";
    public const string NotScheduled = "Ca thi không còn ở trạng thái chờ thi (SCHEDULED). Chỉ được sửa hoặc xóa ca chưa kích hoạt.";
    public const string Published = "Ca thi đã công bố đề thi. Không thể sửa hoặc xóa ca đã công bố.";
    public const string Locked = "Ca thi đã khóa dữ liệu. Không thể sửa hoặc xóa ca đã khóa.";
    public const string Finalized = "Ca thi đã chốt điểm. Không thể sửa hoặc xóa ca đã chốt điểm.";
    public const string HasTickets = "Ca thi đã có vé thi được phân cho sinh viên. Không thể xóa; cần giữ ca và danh sách dự thi.";
    public const string HasUsedTickets = "Ca thi đã có vé thi được sử dụng, bài nộp hoặc điểm được khóa/công bố. Không thể sửa cấu hình ca thi.";
    public const string HasParticipants = "Ca thi đã có sinh viên tham gia. Không thể sửa hoặc xóa; cần giữ lịch sử tham gia.";
    public const string HasSubmissions = "Ca thi đã có bài làm của sinh viên. Không thể sửa hoặc xóa; cần giữ bài làm và dữ liệu chấm điểm.";
    public const string HasAntiCheatLogs = "Ca thi đã có nhật ký giám sát hoặc vi phạm. Không thể sửa hoặc xóa; cần giữ bằng chứng thi.";
    public const string HasPasswords = "Ca thi đã phát sinh mật khẩu vào thi. Không thể sửa hoặc xóa ca đã có lịch sử mở thi.";

    public static string? Code(string error) => error switch
    {
        AlreadyStarted => "SHIFT_ALREADY_STARTED",
        NotScheduled => "SHIFT_NOT_SCHEDULED",
        Published => "SHIFT_EXAM_PUBLISHED",
        Locked => "SHIFT_LOCKED",
        Finalized => "SHIFT_GRADES_FINALIZED",
        HasTickets => "SHIFT_HAS_TICKETS",
        HasUsedTickets => "SHIFT_HAS_USED_TICKETS",
        HasParticipants => "SHIFT_HAS_PARTICIPANTS",
        HasSubmissions => "SHIFT_HAS_SUBMISSIONS",
        HasAntiCheatLogs => "SHIFT_HAS_ANTI_CHEAT_LOGS",
        HasPasswords => "SHIFT_HAS_PASSWORDS",
        _ => null
    };
}
