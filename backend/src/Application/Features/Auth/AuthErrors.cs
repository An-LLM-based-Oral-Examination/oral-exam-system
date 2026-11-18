namespace OralExamination.Application.Features.Auth;

public static class AuthErrors
{
    public const string GoogleTokenInvalid = "ID Token của Google không hợp lệ hoặc không thuộc miền trường được phép.";
    public const string GoogleNonceInvalid = "Phiên đăng nhập Google đã hết hạn hoặc đã được sử dụng. Vui lòng thử lại.";
    public const string InvalidCredentials = "Tên đăng nhập hoặc mật khẩu không chính xác.";
    public const string AccountNotFound = "Tài khoản không tồn tại.";
    public const string WrongPassword = "Mật khẩu không đúng.";
    public const string AccountLocked = "Tài khoản đã bị khóa.";
    public const string LoginTemporarilyLocked = "Đăng nhập tạm khóa do quá nhiều lần thử sai. Vui lòng thử lại sau.";
    public const string PasswordNotSet = "Tài khoản chưa được cấp mật khẩu.";
    public const string CurrentPasswordWrong = "Mật khẩu hiện tại không đúng.";
    public const string PasswordUnchanged = "Mật khẩu mới phải khác mật khẩu hiện tại.";
}
