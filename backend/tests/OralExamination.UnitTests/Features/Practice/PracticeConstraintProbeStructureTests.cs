using System;
using System.Reflection;
using FluentAssertions;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class PracticeConstraintProbeStructureTests
{
    private static void Verify(string definition)
    {
        var method = typeof(PostgresSchemaReconciler).GetMethod(
            "ValidatePracticeAnswersCheckConstraintDefinition",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        method!.Invoke(null, new object[] { definition });
    }

    [Theory]
    [InlineData("CHECK (((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL)))")]
    [InlineData("CHECK ((((NOT is_follow_up) AND (parent_answer_id IS NULL)) OR (is_follow_up AND (parent_answer_id IS NOT NULL))))")]
    public void Probe_accepts_correct_parenthesized_contract(string definition) => Verify(definition);

    [Theory]
    [InlineData("CHECK (((is_follow_up = false AND (parent_answer_id IS NULL OR is_follow_up = true)) AND parent_answer_id IS NOT NULL))")]
    [InlineData("CHECK ((is_follow_up = false AND parent_answer_id IS NULL OR is_follow_up = true) AND parent_answer_id IS NOT NULL)")]
    [InlineData("CHECK (((is_follow_up = false AND parent_answer_id IS NULL) OR (is_follow_up = true AND parent_answer_id IS NOT NULL)) OR TRUE)")]
    public void Probe_rejects_wrong_grouping_or_extra_branch(string definition)
    {
        Action act = () => Verify(definition);
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*[FAIL CLOSED]*");
    }
}
