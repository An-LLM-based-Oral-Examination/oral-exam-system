namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Mã hóa đối xứng có xác thực (AEAD) cho mật khẩu tham gia lớp học (classes.join_password_protected),
/// giúp Giảng viên phụ trách lớp xem lại và sao chép mật khẩu khi chia sẻ với sinh viên
/// mà không lưu văn bản thuần (plaintext) trong cơ sở dữ liệu.
/// </summary>
public interface IClassJoinPasswordProtector
{
    string? Protect(string? plainPassword);
    string? Unprotect(string? protectedPassword);
}
