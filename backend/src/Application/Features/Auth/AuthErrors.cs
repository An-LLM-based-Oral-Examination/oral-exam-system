namespace OralExamination.Application.Features.Auth;

public static class AuthErrors
{
    public const string AccountNotFound = "Tài khoản không tồn tại.";
    public const string WrongPassword = "Mật khẩu không đúng.";
    public const string AccountLocked = "Tài khoản đã bị khóa.";
    public const string PasswordNotSet = "Tài khoản chưa được cấp mật khẩu.";
    public const string CurrentPasswordWrong = "Mật khẩu hiện tại không đúng.";
    public const string PasswordUnchanged = "Mật khẩu mới phải khác mật khẩu hiện tại.";
}
