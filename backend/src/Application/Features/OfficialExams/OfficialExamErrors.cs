namespace OralExamination.Application.Features.OfficialExams;

public static class OfficialExamErrors
{
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này.";
    public const string CourseNotFound = "Môn học không tồn tại.";
    public const string StructureNotFound = "Cấu trúc đề không thuộc môn học này.";
    public const string SessionNotFound = "Kỳ thi không tồn tại.";
    public const string ShiftNotFound = "Ca thi không tồn tại.";
    public const string TicketNotFound = "Vé thi không tồn tại.";
    public const string StudentNotFound = "Sinh viên không tồn tại.";
    public const string ProctorInvalid = "Người coi thi phải là giảng viên hoặc giám thị đang hoạt động.";
    public const string SeatTaken = "Ghế này đã được gán trong ca thi.";
    public const string StudentAlreadyAssigned = "Sinh viên đã có vé trong ca thi này.";
    public const string WrongWorkstation = "Sai máy trạm.";
    public const string WorkstationUnknown = "Không xác định được địa chỉ máy trạm.";
    public const string TicketNotScheduled = "Vé thi không ở trạng thái chờ điểm danh.";
    public const string ShiftNotOpen = "Không trong thời gian ca thi.";
    public const string ShiftClosed = "Ca thi không còn mở điểm danh.";
    public const string WorkstationTicketNotFound = "Không có vé thi trên máy trạm này.";
    public const string OverlappingShifts = "Có nhiều ca thi trùng giờ trên máy trạm này.";
    public const string SeatOutOfRange = "Số ghế phải từ 1 đến 40.";
    public const string IpInvalid = "Địa chỉ IP máy trạm không hợp lệ.";
    public const string TicketNotInProgress = "Vé thi không ở trạng thái đang thi.";
    public const string AudioAlreadySealed = "Audio của bài thi đã được niêm phong.";
    public const string AudioNotSealed = "Audio của bài thi chưa được tải lên và xác minh SHA-256.";
    public const string AudioObjectNotFound = "Không tìm thấy file audio đã tải lên.";
    public const string AudioObjectInvalid = "File audio không đúng định dạng hoặc vượt quá dung lượng cho phép.";
    public const string AudioHashMismatch = "Mã SHA-256 không khớp với nội dung audio trên kho lưu trữ.";
    public const string AudioObjectKeyMismatch = "Đường dẫn audio không thuộc vé thi này.";
    public const string ExamSetNotAssigned = "Vé thi chưa được gán bộ đề.";
    public const string SubmissionQuestionsMismatch = "Danh sách câu trả lời không khớp với bộ đề của vé thi.";
    public const string SubmissionAlreadyExists = "Bài thi đã được nộp trước đó.";
}
