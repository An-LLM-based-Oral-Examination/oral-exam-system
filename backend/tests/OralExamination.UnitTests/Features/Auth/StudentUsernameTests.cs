using FluentAssertions;
using OralExamination.Domain.Identity;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class StudentUsernameTests
{
    [Theory]
    [InlineData("Nguyễn Quang Thành", "SE184236", "ThanhNQSE184236")]
    [InlineData("Nguyễn Trọng Tốt", "SE181863", "TotNTSE181863")]
    [InlineData("Le Vu Hoang", "SE170001", "HoangLVSE170001")]
    [InlineData("Tran Thi Mai", "SE170002", "MaiTTSE170002")]
    [InlineData("Lê Vũ An", "se180001", "AnLVSE180001")]
    [InlineData("Nguyễn An", "SE180001", "AnNSE180001")]
    [InlineData("Đặng Thị Đan", "se190001", "DanDTSE190001")]
    [InlineData("Nguyễn Văn A", "SE180101", "ANVSE180101")]
    public void TryCreate_BuildsFullGivenNameAndAllPrefixInitialsWithStudentCode(string fullName, string studentCode, string expected)
    {
        StudentUsername.TryCreate(fullName, studentCode, out var username).Should().BeTrue();
        username.Should().Be(expected);
    }

    [Fact]
    public void TryCreate_IncludesAllMiddleNameInitials()
    {
        StudentUsername.TryCreate("Nguyễn Thị Cẩm Hương", "SE181863", out var username).Should().BeTrue();
        username.Should().Be("HuongNTCSE181863");
    }

    [Theory]
    [InlineData("Thành", "SE184236")]
    [InlineData("   ", "SE184236")]
    [InlineData("Nguyễn Quang Thành", "SE1842")]
    [InlineData("Nguyễn Quang Thành", "184236")]
    [InlineData("Nguyễn Quang Thành", "SE1842369")]
    [InlineData("Nguyễn Quang Thành", "SE-184236")]
    [InlineData("Nguyễn Quang Thành", "SE 184236")]
    [InlineData("Nguyễn Quang Thành", "SE@184236")]
    [InlineData("Nguyễn Quang Thành", "SÉ184236")]
    public void TryCreate_RejectsInvalidFullNameOrStudentCode(string fullName, string studentCode)
    {
        StudentUsername.TryCreate(fullName, studentCode, out var username).Should().BeFalse();
        username.Should().BeEmpty();
    }

    [Fact]
    public void TryCreate_RejectsWhenResultExceedsMaxLength()
    {
        var veryLongGivenName = new string('a', 45);
        StudentUsername.TryCreate($"Nguyen {veryLongGivenName}", "SE184236", out var username).Should().BeFalse();
        username.Should().BeEmpty();
    }

    [Theory]
    [InlineData(" ThanhNQSE184236 ", "se184236")]
    [InlineData("thanhnqse184236", "se184236")]
    [InlineData("THANHNQSE184236", "se184236")]
    [InlineData(" totntSE181863 ", "se181863")]
    [InlineData("hoalvse170001", "se170001")]
    [InlineData("SE170001", "")]
    public void TryExtractStudentCode_ReadsTheTrailingCode(string username, string expectedCode)
    {
        var found = StudentUsername.TryExtractStudentCode(username, out var studentCode);
        if (expectedCode.Length == 0)
        {
            found.Should().BeFalse();
            return;
        }

        found.Should().BeTrue();
        studentCode.Should().Be(expectedCode);
    }
}
