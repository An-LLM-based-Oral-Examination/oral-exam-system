namespace OralExamination.Application.Common.Auth;

public sealed class GoogleIdTokenRejectedException()
    : Exception("ID Token của Google không hợp lệ.");
