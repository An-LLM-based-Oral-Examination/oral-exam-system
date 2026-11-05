namespace OralExamination.Application.Features.InClassShifts;

public static class InClassShiftErrors
{
    public const string ShiftNotFound = "Không tìm thấy ca thi trong lớp.";
    public const string ClassNotFound = "Không tìm thấy lớp học phần hoặc lớp đã ngừng hoạt động.";
    public const string LecturerNotAuthorized = "Giảng viên không có quyền quản lý ca thi của lớp học này.";
    public const string InvalidTimeRange = "Thời gian kết thúc ca thi phải lớn hơn hoặc bằng thời gian bắt đầu.";
    public const string ShiftLocked = "Ca thi đã bị khóa một chiều, không thể thay đổi dữ liệu hoặc điểm số.";
    public const string ShiftCancelled = "Ca thi đã bị hủy.";
    public const string ShiftNotOpen = "Ca thi không ở trạng thái mở để sinh viên tham gia.";
    public const string StudentNotEnrolled = "Sinh viên không thuộc danh sách lớp học này.";
    public const string InvalidOtp = "Mã OTP không chính xác hoặc đã hết hạn (chu kỳ 5 phút).";
    public const string ParticipantNotFound = "Sinh viên chưa tham gia ca thi này.";
    public const string SubmissionNotFound = "Không tìm thấy bài nộp ca thi.";
    public const string DuplicateSubmission = "Sinh viên đã nộp bài cho ca thi này, không thể nộp trùng lặp.";
    public const string QuestionMismatch = "Câu hỏi nộp bài không khớp với đề câu hỏi đã được hệ thống phân bổ cho sinh viên.";
    public const string InvalidCourseQuestion = "Câu hỏi không tồn tại, chưa được phê duyệt hoặc không thuộc môn học của lớp này.";
    public const string InsufficientQuestionPool = "Ngân hàng câu hỏi luyện tập của môn học chưa đủ số lượng câu hỏi đã duyệt (APPROVED) để phân bổ cho ca thi.";
    public const string InvalidScoreRange = "Điểm số phải nằm trong khoảng từ 0.00 đến 10.00 điểm.";
    public const string EmptyTranscript = "Nội dung bài nói không được để trống.";
}
