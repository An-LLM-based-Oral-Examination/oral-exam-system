namespace OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;

public static class PublishGradesErrors
{
    public const string ActorNotFound = "Giảng viên không tồn tại trong hệ thống.";
    public const string Forbidden = "Người dùng không có quyền công bố điểm ca thi.";
    public const string ShiftNotFound = "Ca thi không tồn tại trong hệ thống.";
    public const string ShiftNotEnded = "Không thể công bố điểm khi ca thi chưa kết thúc.";
    public const string NoTickets = "Ca thi không có vé thi nào để công bố điểm.";
    public const string AlreadyPublished = "Ca thi đã được công bố hoặc có trạng thái khóa không nhất quán.";
    public const string IncompleteGrades = "Không thể công bố điểm. Còn sinh viên chưa có điểm hoàn chỉnh.";
}
