using System;
using System.Collections.Generic;
using FluentAssertions;
using OralExamination.Application.Features.QuestionBank.Commands.BatchSubmitQuestionsForReview;
using Xunit;

namespace OralExamination.UnitTests.Features.QuestionBank.Validators;

public class BatchSubmitQuestionsForReviewCommandValidatorTests
{
    private readonly BatchSubmitQuestionsForReviewCommandValidator _validator;

    public BatchSubmitQuestionsForReviewCommandValidatorTests()
    {
        _validator = new BatchSubmitQuestionsForReviewCommandValidator();
    }

    private BatchSubmitQuestionsForReviewCommand CreateValidCommand(
        int questionCount = 3,
        string? submissionNotes = "Kính gửi Bộ Môn thẩm định bộ đề thi môn PRN231")
    {
        var questionIds = new List<Guid>();
        for (int i = 0; i < questionCount; i++)
        {
            questionIds.Add(Guid.NewGuid());
        }

        return new BatchSubmitQuestionsForReviewCommand(
            CourseId: Guid.NewGuid(),
            LecturerId: Guid.NewGuid(),
            QuestionIds: questionIds,
            SubmissionNotes: submissionNotes
        );
    }

    [Fact(DisplayName = "1. Pass khi câu lệnh gửi duyệt hợp lệ với danh sách câu hỏi và ghi chú đầy đủ")]
    public void Validate_Should_Pass_When_Command_Is_Valid()
    {
        var command = CreateValidCommand(questionCount: 3);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact(DisplayName = "2. Pass khi SubmissionNotes là null hoặc rỗng")]
    public void Validate_Should_Pass_When_SubmissionNotes_Is_Null_Or_Empty()
    {
        var commandNullNotes = CreateValidCommand(submissionNotes: null);
        var resultNull = _validator.Validate(commandNullNotes);
        resultNull.IsValid.Should().BeTrue();

        var commandEmptyNotes = CreateValidCommand(submissionNotes: "");
        var resultEmpty = _validator.Validate(commandEmptyNotes);
        resultEmpty.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "3. Fail khi CourseId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_CourseId_Is_Empty()
    {
        var command = CreateValidCommand() with { CourseId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BatchSubmitQuestionsForReviewCommand.CourseId) &&
                                            e.ErrorMessage.Contains("môn học"));
    }

    [Fact(DisplayName = "4. Fail khi LecturerId rỗng (Guid.Empty)")]
    public void Validate_Should_Fail_When_LecturerId_Is_Empty()
    {
        var command = CreateValidCommand() with { LecturerId = Guid.Empty };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BatchSubmitQuestionsForReviewCommand.LecturerId) &&
                                            e.ErrorMessage.Contains("giảng viên"));
    }

    [Fact(DisplayName = "5. Fail khi QuestionIds là danh sách rỗng")]
    public void Validate_Should_Fail_When_QuestionIds_Is_Empty()
    {
        var command = CreateValidCommand() with { QuestionIds = new List<Guid>() };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BatchSubmitQuestionsForReviewCommand.QuestionIds) &&
                                            e.ErrorMessage.Contains("không được để trống"));
    }

    [Fact(DisplayName = "6. Fail khi QuestionIds là null")]
    public void Validate_Should_Fail_When_QuestionIds_Is_Null()
    {
        var command = new BatchSubmitQuestionsForReviewCommand(
            CourseId: Guid.NewGuid(),
            LecturerId: Guid.NewGuid(),
            QuestionIds: null!,
            SubmissionNotes: "Ghi chú hợp lệ"
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BatchSubmitQuestionsForReviewCommand.QuestionIds));
    }

    [Fact(DisplayName = "7. Fail khi SubmissionNotes vượt quá giới hạn 1000 ký tự")]
    public void Validate_Should_Fail_When_SubmissionNotes_Exceeds_1000_Characters()
    {
        string longNote = new string('N', 1001);
        var command = CreateValidCommand(submissionNotes: longNote);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BatchSubmitQuestionsForReviewCommand.SubmissionNotes) &&
                                            e.ErrorMessage.Contains("1000 ký tự"));
    }
}
