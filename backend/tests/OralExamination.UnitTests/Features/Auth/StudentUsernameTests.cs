using FluentAssertions;
using OralExamination.Domain.Identity;
using Xunit;

namespace OralExamination.UnitTests.Features.Auth;

public class StudentUsernameTests
{
    [Theory]
    [InlineData("Nguyễn Trọng Tốt", "SE181863", "totntse181863")]
    [InlineData("Le Vu Hoang", "SE170001", "hoalvse170001")]
    [InlineData("Tran Thi Mai", "SE170002", "maittse170002")]
    [InlineData("Lê Vũ An", "se180001", "anlvse180001")]
    [InlineData("Nguyễn Tốt", "SE181863", "totnse181863")]
    public void TryCreate_BuildsPrefixFromGivenFamilyAndFirstMiddleName(string fullName, string studentCode, string expected)
    {
        StudentUsername.TryCreate(fullName, studentCode, out var username).Should().BeTrue();
        username.Should().Be(expected);
    }

    [Fact]
    public void TryCreate_UsesOnlyTheFirstMiddleName()
    {
        StudentUsername.TryCreate("Nguyễn Thị Cẩm Hương", "SE181863", out var username).Should().BeTrue();
        username.Should().Be("huontse181863");
    }

    [Theory]
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
