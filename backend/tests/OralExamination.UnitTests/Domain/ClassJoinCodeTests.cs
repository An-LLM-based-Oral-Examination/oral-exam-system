using FluentAssertions;
using OralExamination.Domain.Entities;
using Xunit;

namespace OralExamination.UnitTests.Domain;

public sealed class ClassJoinCodeTests
{
    [Fact]
    public void NewClass_WithoutExplicitJoinCode_HasPrefixedSixteenCharacterJoinCode()
    {
        var classroom = new Class();

        classroom.JoinCode.Should().MatchRegex("^CLS[A-F0-9]{13}$");
    }

    [Fact]
    public void NewClass_WithExplicitJoinCode_PreservesProvidedCode()
    {
        var classroom = new Class { JoinCode = "SE1801NET" };

        classroom.JoinCode.Should().Be("SE1801NET");
    }
}
