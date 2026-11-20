using System.Linq;
using FluentAssertions;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Services;

public class QuestionGenerationGuidanceProviderTests
{
    [Fact]
    public void Swr_HasScopeForAllNineClos_AndOnlyReturnsSelectedScope()
    {
        var provider = new QuestionGenerationGuidanceProvider();
        var codes = Enumerable.Range(1, 9).Select(i => $"CLO{i}").ToArray();
        var all = provider.Get("swr302", codes)!;
        all.Clos.Select(c => c.Code).Should().Equal(codes);
        all.Clos.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Topics) && !string.IsNullOrWhiteSpace(c.Boundaries));
        var selected = provider.Get(" SWR302 ", new[] { " clo6 " })!;
        selected.Clos.Should().ContainSingle().Which.Code.Should().Be("CLO6");
        selected.Clos.Single().Topics.Should().Contain("Object-oriented Design");
        selected.Clos.Single().Boundaries.Should().Contain("Design Pattern");
        selected.Clos.Should().NotBeSameAs(all.Clos);
        provider.Get("SWR302", codes)!.Clos.Should().HaveCount(9);
        provider.Get("SWR302", new[] { "CLO99" })!.Clos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("SWT301")]
    [InlineData("PRF192")]
    [InlineData("CSD201")]
    [InlineData("DBI202")]
    public void UnconfiguredCourses_UseExistingCloContext(string code)
    {
        new QuestionGenerationGuidanceProvider().Get(code, new[] { "CLO6" }).Should().BeNull();
    }
}
