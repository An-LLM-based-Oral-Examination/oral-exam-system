using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.MockExams;

public class SubmitMockExamSessionCommandHandlerTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    private (Course Course, PracticeQuestion Q1, PracticeQuestion Q2) SeedCourseAndQuestions(OralExamDbContext context)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3
        };
        context.Courses.Add(course);

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Barem Kiến trúc phần mềm",
            TotalMaxScore = 10.0m
        };
        context.Rubrics.Add(rubric);

        var crit1 = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Kiến thức Clean Architecture",
            MaxScore = 5.0m,
            Weight = 0.5m,
            OrderIndex = 1
        };
        var crit2 = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "Phân tích Trade-off",
            MaxScore = 5.0m,
            Weight = 0.5m,
            OrderIndex = 2
        };
        context.RubricCriteria.AddRange(crit1, crit2);

        var q1 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi 1",
            Content = "Nêu 4 tầng của Clean Architecture",
            SampleAnswer = "Domain, Application, Infrastructure, Presentation",
            Difficulty = "easy",
            BloomLevel = "Remember"
        };
        var q2 = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Câu hỏi 2",
            Content = "So sánh CQRS và CRUD",
            SampleAnswer = "CQRS tách biệt đọc và ghi, phù hợp hệ thống phức tạp",
            Difficulty = "medium",
            BloomLevel = "Understand"
        };
        context.PracticeQuestions.AddRange(q1, q2);
        context.SaveChanges();

        return (course, q1, q2);
    }

    [Fact(DisplayName = "1. Nộp bài đúng hạn thành công: AI chấm điểm CoT, cập nhật total_score và phân loại xếp loại chính xác")]
    public async Task Handle_Should_Grade_Session_And_Produce_Scorecard_When_Submitted_On_Time()
    {
        using var context = CreateDbContext();
        var (course, q1, q2) = SeedCourseAndQuestions(context);
        var studentId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10), // Còn hạn 10 phút
            Status = "in_progress"
        };

        var a1 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            AnswerText = "Clean Architecture gồm Domain, Application, Infrastructure và Web API.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-15)
        };

        var a2 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q2.Id,
            AnswerText = "CQRS chia tách Command và Query giúp tối ưu hóa hiệu năng đọc ghi riêng biệt.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-5)
        };

        session.Answers.Add(a1);
        session.Answers.Add(a2);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.SetupSequence(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.5m,
                ConfidenceScore = 0.95m,
                Feedback = "Trả lời tốt, giải thích rõ các tầng.",
                Details = new List<GradingDetailDto>
                {
                    new() { CriterionId = q1.Rubric.Criteria.First().Id.ToString(), Score = 4.5m, Comment = "Đầy đủ" },
                    new() { CriterionId = q1.Rubric.Criteria.Last().Id.ToString(), Score = 4.0m, Comment = "Tốt" }
                }
            })
            .ReturnsAsync(new AiGradingResult
            {
                Score = 9.5m,
                ConfidenceScore = 0.98m,
                Feedback = "Phân tích trade-off sâu sắc.",
                Details = new List<GradingDetailDto>
                {
                    new() { CriterionId = q2.Rubric.Criteria.First().Id.ToString(), Score = 5.0m, Comment = "Xuất sắc" },
                    new() { CriterionId = q2.Rubric.Criteria.Last().Id.ToString(), Score = 4.5m, Comment = "Tốt" }
                }
            });

        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var command = new SubmitMockExamSessionCommand(session.Id, studentId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var scorecard = result.Value;
        scorecard.Should().NotBeNull();
        scorecard.SessionId.Should().Be(session.Id);
        scorecard.CourseCode.Should().Be("SWD392");
        scorecard.TotalQuestions.Should().Be(2);
        scorecard.AnsweredQuestions.Should().Be(2);
        // (8.5 + 9.5) / 2 = 9.00
        scorecard.TotalScore.Should().Be(9.00m);
        scorecard.Classification.Should().Be("Xuất sắc");
        scorecard.IsOverdue.Should().BeFalse();
        scorecard.Questions.Should().HaveCount(2);

        // Kiểm tra persistence
        var updatedSession = await context.MockExamSessions.FindAsync(session.Id);
        updatedSession!.Status.Should().Be("graded");
        updatedSession.TotalScore.Should().Be(9.00m);

        var evaluations = await context.AiEvaluations.Where(e => e.MockExamAnswerId != null).ToListAsync();
        evaluations.Should().HaveCount(2);
    }

    [Fact(DisplayName = "2. Nộp quá hạn (Master Timer overdue): tự động khóa ca thi, chỉ chấm câu đã làm, câu chưa làm nhận 0đ")]
    public async Task Handle_Should_Auto_Lock_And_Assign_Zero_To_Unanswered_Questions_When_Overdue()
    {
        using var context = CreateDbContext();
        var (course, q1, q2) = SeedCourseAndQuestions(context);
        var studentId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-40),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Quá hạn 5 phút (> 10s grace)
            Status = "in_progress"
        };

        var a1 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            AnswerText = "Đã kịp trả lời câu 1.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-20)
        };

        var a2 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q2.Id,
            AnswerText = null,
            Status = "pending",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-40)
        };

        session.Answers.Add(a1);
        session.Answers.Add(a2);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.0m,
                ConfidenceScore = 0.90m,
                Feedback = "Câu 1 trả lời tốt.",
                Details = new List<GradingDetailDto>()
            });

        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var command = new SubmitMockExamSessionCommand(session.Id, studentId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var scorecard = result.Value;
        scorecard.IsOverdue.Should().BeTrue();
        scorecard.AnsweredQuestions.Should().Be(1);
        scorecard.TotalQuestions.Should().Be(2);
        // (8.0 + 0) / 2 = 4.00
        scorecard.TotalScore.Should().Be(4.00m);
        scorecard.Classification.Should().Be("Yếu");

        var q2Score = scorecard.Questions.FirstOrDefault(q => q.QuestionId == q2.Id);
        q2Score.Should().NotBeNull();
        q2Score!.Score.Should().Be(0.0m);
        q2Score.Feedback.Should().Contain("Hết giờ làm bài - Không có câu trả lời");
    }

    [Fact(DisplayName = "3. Chặn nộp trùng khi ca thi đã ở trạng thái submitted hoặc graded")]
    public async Task Handle_Should_Fail_When_Session_Already_Submitted_Or_Graded()
    {
        using var context = CreateDbContext();
        var (course, _, _) = SeedCourseAndQuestions(context);
        var studentId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(0),
            Status = "graded"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var command = new SubmitMockExamSessionCommand(session.Id, studentId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã được nộp và chấm điểm trước đó");
    }

    [Fact(DisplayName = "4. Chặn nộp bài khi sinh viên không sở hữu ca thi hoặc ca thi không tồn tại")]
    public async Task Handle_Should_Fail_When_User_Not_Owner_Or_Not_Found()
    {
        using var context = CreateDbContext();
        var (course, _, _) = SeedCourseAndQuestions(context);
        var ownerId = Guid.NewGuid();
        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = ownerId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            Status = "in_progress"
        };
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var aiMock = new Mock<IAiGradingService>();
        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);

        // Case 1: Wrong student
        var wrongStudentCmd = new SubmitMockExamSessionCommand(session.Id, Guid.NewGuid());
        var res1 = await handler.Handle(wrongStudentCmd, CancellationToken.None);
        res1.IsSuccess.Should().BeFalse();
        res1.Error.Should().Contain("không có quyền");

        // Case 2: Session not found
        var notFoundCmd = new SubmitMockExamSessionCommand(Guid.NewGuid(), ownerId);
        var res2 = await handler.Handle(notFoundCmd, CancellationToken.None);
        res2.IsSuccess.Should().BeFalse();
        res2.Error.Should().Contain("không tồn tại");
    }

    [Fact(DisplayName = "5. SubmitMockExamSessionCommandValidator kiểm tra tính hợp lệ tham số")]
    public void Validator_Should_Enforce_Parameters()
    {
        var validator = new SubmitMockExamSessionCommandValidator();
        var invalid = new SubmitMockExamSessionCommand(Guid.Empty, Guid.Empty);

        var res = validator.Validate(invalid);
        res.IsValid.Should().BeFalse();
        res.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitMockExamSessionCommand.SessionId));
        res.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitMockExamSessionCommand.StudentId));
    }

    [Fact(DisplayName = "6. Follow-up trả lời yếu hơn câu gốc: áp dụng trọng số 70% câu gốc + 30% follow-up và đồng bộ tổng tiêu chí con")]
    public async Task Handle_Should_Apply_Weighted_Score_When_FollowUp_Is_Lower_And_Does_Not_Improve_Criteria()
    {
        using var context = CreateDbContext();
        var (course, q1, _) = SeedCourseAndQuestions(context);
        var studentId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-15),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Status = "in_progress"
        };

        var mainAnswer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            OrderIndex = 1,
            IsFollowUp = false,
            AnswerText = "Câu trả lời chính đạt 8.0 điểm.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var followUpAnswer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            ParentAnswerId = mainAnswer.Id,
            OrderIndex = 1,
            IsFollowUp = true,
            PromptText = "Giải thích sâu hơn về Dependency Rule?",
            AnswerText = "Câu trả lời phụ lúng túng chỉ đạt 3.0 điểm.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-5)
        };

        session.Answers.Add(mainAnswer);
        session.Answers.Add(followUpAnswer);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var crit1Id = q1.Rubric.Criteria.First().Id.ToString();
        var crit2Id = q1.Rubric.Criteria.Last().Id.ToString();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.SetupSequence(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult
            {
                Score = 8.0m,
                ConfidenceScore = 0.90m,
                Feedback = "Câu gốc trả lời tốt (4.0 + 4.0).",
                Details = new List<GradingDetailDto>
                {
                    new() { CriterionId = crit1Id, Score = 4.0m, Comment = "Tốt" },
                    new() { CriterionId = crit2Id, Score = 4.0m, Comment = "Tốt" }
                }
            })
            .ReturnsAsync(new AiGradingResult
            {
                Score = 3.0m,
                ConfidenceScore = 0.85m,
                Feedback = "Câu hỏi phụ trả lời yếu (1.5 + 1.5).",
                Details = new List<GradingDetailDto>
                {
                    new() { CriterionId = crit1Id, Score = 1.5m, Comment = "Yếu" },
                    new() { CriterionId = crit2Id, Score = 1.5m, Comment = "Yếu" }
                }
            });

        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var result = await handler.Handle(new SubmitMockExamSessionCommand(session.Id, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var scorecard = result.Value;
        // 70% * 8.0 + 30% * 3.0 = 5.6 + 0.9 = 6.50m
        scorecard.TotalScore.Should().Be(6.50m);
        var mainQuestionDto = scorecard.Questions.First(q => !q.IsFollowUp);
        mainQuestionDto.Score.Should().Be(6.50m);
        mainQuestionDto.CriteriaScores.Sum(c => c.Score).Should().Be(6.50m);
    }

    [Fact(DisplayName = "7. Hai câu hỏi Follow-up liên tiếp nhắm vào 2 tiêu chí yếu khác nhau và cộng dồn cập nhật điểm toàn câu chính xác")]
    public async Task Handle_Should_Target_Distinct_Weak_Criteria_Across_Multiple_FollowUps_And_Merge_Question_Score()
    {
        using var context = CreateDbContext();
        var (course, q1, _) = SeedCourseAndQuestions(context);
        var studentId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = course.Id,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-15),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Status = "in_progress"
        };

        var mainAnswer = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            OrderIndex = 1,
            IsFollowUp = false,
            AnswerText = "Câu trả lời chính đạt 4.0 điểm (1.5 + 2.5).",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var followUp1 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            ParentAnswerId = mainAnswer.Id,
            OrderIndex = 1,
            IsFollowUp = true,
            PromptText = "Câu hỏi phụ #1 xoáy vào tiêu chí 1?",
            AnswerText = "Trả lời câu hỏi phụ #1 tốt đạt 4.0/5.0.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-8)
        };

        var followUp2 = new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = q1.Id,
            ParentAnswerId = followUp1.Id,
            OrderIndex = 1,
            IsFollowUp = true,
            PromptText = "Câu hỏi phụ #2 xoáy vào tiêu chí 2?",
            AnswerText = "Trả lời câu hỏi phụ #2 tốt đạt 4.5/5.0.",
            Status = "answered",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-5)
        };

        session.Answers.Add(mainAnswer);
        session.Answers.Add(followUp1);
        session.Answers.Add(followUp2);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var crit1 = q1.Rubric.Criteria.OrderBy(c => c.OrderIndex).First();
        var crit2 = q1.Rubric.Criteria.OrderBy(c => c.OrderIndex).Last();
        var capturedRequests = new List<AiGradingRequest>();

        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AiGradingRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync((AiGradingRequest req, CancellationToken _) =>
            {
                if (!req.IsFollowUp)
                {
                    return new AiGradingResult
                    {
                        Score = 4.0m,
                        ConfidenceScore = 0.90m,
                        Feedback = "Câu gốc đạt 4.0 (1.5 + 2.5).",
                        Details = new List<GradingDetailDto>
                        {
                            new() { CriterionId = crit1.Id.ToString(), Score = 1.5m, Comment = "Cần bổ sung" },
                            new() { CriterionId = crit2.Id.ToString(), Score = 2.5m, Comment = "Trung bình" }
                        }
                    };
                }

                var targetId = req.Criteria.Single().Id;
                var achievedScore = targetId == crit1.Id.ToString() ? 4.0m : 4.5m;
                return new AiGradingResult
                {
                    Score = achievedScore * 2.0m,
                    ConfidenceScore = 0.92m,
                    Feedback = "Cải thiện tốt tiêu chí yếu.",
                    Details = new List<GradingDetailDto>
                    {
                        new() { CriterionId = targetId, Score = achievedScore, Comment = "Đã làm rõ" }
                    }
                };
            });

        var handler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var result = await handler.Handle(new SubmitMockExamSessionCommand(session.Id, studentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedRequests.Should().HaveCount(3);
        // Follow-up #1 nhắm vào crit1 (1.5/5.0 = 0.30 < 2.5/5.0 = 0.50)
        capturedRequests[1].Criteria.Should().ContainSingle(c => c.Id == crit1.Id.ToString());
        // Follow-up #2 phải nhắm vào crit2 (tiêu chí yếu kế tiếp chưa hỏi phụ)
        capturedRequests[2].Criteria.Should().ContainSingle(c => c.Id == crit2.Id.ToString());

        // Điểm câu chính sau khi gộp cả 2 tiêu chí: 4.0 + 4.5 = 8.50m
        result.Value!.TotalScore.Should().Be(8.50m);
        var mainQuestionDto = result.Value.Questions.First(q => !q.IsFollowUp);
        mainQuestionDto.Score.Should().Be(8.50m);
        mainQuestionDto.CriteriaScores.Sum(c => c.Score).Should().Be(8.50m);
    }
}
