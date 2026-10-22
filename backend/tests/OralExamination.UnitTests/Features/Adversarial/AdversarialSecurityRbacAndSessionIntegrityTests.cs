using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.API.Hubs;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamSession;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetPracticeSession;
using OralExamination.Application.Features.Practice.Queries.GetStudentPracticeHistory;
using OralExamination.Application.Features.Student.DTOs;
using OralExamination.Application.Features.Student.Queries.GetStudentDashboard;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử đối kháng xác thực 6 hạng mục bảo mật và logic nghiệp vụ:
/// [CORE-01], [MF01-01], [MF01-02], [MF02-01], [RBAC-01], [RBAC-02].
/// </summary>
public class AdversarialSecurityRbacAndSessionIntegrityTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    // =========================================================================
    // 1. [CORE-01] PracticeHub BOLA Guard & SpeechController Auth
    // =========================================================================

    [Fact(DisplayName = "CORE-01A: PracticeHub bắt buộc có [Authorize], cho phép chủ phiên join và chặn sinh viên khác (BOLA)")]
    public async Task PracticeHub_JoinSession_EnforcesAuthorizeAndSessionOwnership()
    {
        // 1. Kiểm tra [Authorize] trên PracticeHub
        var hasAuthorize = typeof(PracticeHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Any();
        hasAuthorize.Should().BeTrue("PracticeHub bắt buộc phải có [Authorize] để ngăn kết nối ẩn danh nghe lén kết quả chấm điểm");

        using var context = CreateDbContext();
        var ownerStudentId = Guid.NewGuid();
        var attackerStudentId = Guid.NewGuid();
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = ownerStudentId,
            CourseId = Guid.NewGuid(),
            PracticeMode = "per_question",
            Status = "in_progress"
        };
        context.PracticeSessions.Add(session);
        await context.SaveChangesAsync();

        var groupsMock = new Mock<IGroupManager>();
        var hub = new PracticeHub(context)
        {
            Groups = groupsMock.Object
        };

        // 2. Sinh viên B (attacker) cố gắng join vào session của Sinh viên A -> Bị chặn HubException FORBIDDEN
        var attackerContext = new Mock<HubCallerContext>();
        attackerContext.Setup(c => c.ConnectionId).Returns("conn-attacker");
        attackerContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, attackerStudentId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        }, "TestAuth")));
        hub.Context = attackerContext.Object;

        var actAttacker = async () => await hub.JoinSession(session.Id.ToString());
        await actAttacker.Should().ThrowAsync<HubException>()
            .WithMessage("*FORBIDDEN*");
        groupsMock.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // 3. Sinh viên A (chính chủ) join vào session của mình -> Thành công
        var ownerContext = new Mock<HubCallerContext>();
        ownerContext.Setup(c => c.ConnectionId).Returns("conn-owner");
        ownerContext.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ownerStudentId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        }, "TestAuth")));
        hub.Context = ownerContext.Object;

        await hub.JoinSession(session.Id.ToString());
        groupsMock.Verify(g => g.AddToGroupAsync("conn-owner", $"session_{session.Id:D}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "CORE-01B: SpeechController tuyệt đối không gắn [AllowAnonymous] trên các endpoints TTS và STT")]
    public void SpeechController_TtsAndSttEndpoints_MustNotAllowAnonymous()
    {
        var controllerType = typeof(SpeechController);
        controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().NotBeEmpty();

        var protectedMethods = new[]
        {
            nameof(SpeechController.SynthesizeSpeechPost),
            nameof(SpeechController.SynthesizeSpeechGet),
            nameof(SpeechController.TranscribeSpeech)
        };

        foreach (var methodName in protectedMethods)
        {
            var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            method.Should().NotBeNull($"Phương thức {methodName} phải tồn tại trên SpeechController");
            var allowAnonymous = method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);
            allowAnonymous.Should().BeEmpty($"Endpoint {methodName} không được phép mở [AllowAnonymous] gây lộ quota AI");
        }
    }

    // =========================================================================
    // 2. [MF01-01] & [MF01-02] PracticeSession Drawn Question Persistence & Full-Session Timeout Scoring
    // =========================================================================

    [Fact(DisplayName = "MF01-01A: F5 (GetPracticeSession) khi chưa nộp câu trả lời nào vẫn giữ nguyên danh sách câu hỏi đã bốc")]
    public async Task PracticeSession_ReloadBeforeAnswering_PreservesExactDrawnQuestions()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Web APIs",
            TranscriptBufferSeconds = 60,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.f5@fpt.edu.vn",
            FullName = "Nguyễn Văn F5",
            Role = UserRole.Student,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric PRN231",
            TotalMaxScore = 10.0m,
            IsActive = true
        };
        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);

        var difficulties = new[] { "easy", "medium", "hard" };
        foreach (var diff in difficulties)
        {
            for (int i = 1; i <= 4; i++)
            {
                context.PracticeQuestions.Add(new PracticeQuestion
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    RubricId = rubric.Id,
                    Title = $"Câu {diff} #{i}",
                    Content = $"Nội dung câu hỏi {diff} #{i}",
                    Difficulty = diff,
                    IsActive = true
                });
            }
        }
        await context.SaveChangesAsync();

        var startHandler = new StartPracticeSessionCommandHandler(context);
        var startRes = await startHandler.Handle(new StartPracticeSessionCommand(
            StudentId: student.Id,
            CourseId: course.Id,
            Difficulty: "progressive",
            QuestionCount: 5,
            IsFullSession: true,
            Topic: null), CancellationToken.None);

        startRes.IsSuccess.Should().BeTrue();
        var drawnQuestionIds = startRes.Value!.Questions.Select(q => q.QuestionId).ToList();
        drawnQuestionIds.Should().HaveCount(5);

        // Sinh viên bấm F5 khi chưa nộp bất kỳ câu trả lời nào
        var getHandler = new GetPracticeSessionQueryHandler(context);
        var getRes = await getHandler.Handle(
            new GetPracticeSessionQuery(startRes.Value.SessionId, student.Id, IsElevatedRole: false),
            CancellationToken.None);

        getRes.IsSuccess.Should().BeTrue();
        var reloadedQuestionIds = getRes.Value.Questions.Select(q => q.QuestionId).ToList();
        reloadedQuestionIds.Should().Equal(drawnQuestionIds, "Danh sách và thứ tự câu hỏi khi F5 phải trùng khớp 100% với đề đã bốc lúc tạo phiên");
    }

    [Fact(DisplayName = "MF01-01B & MF01-02: Full-Session đóng do Timeout tính 0đ cho câu chưa làm; Lịch sử có Follow-up đảm bảo AnsweredQuestions <= TotalQuestions")]
    public async Task PracticeHistoryAndDashboard_FullSessionTimeoutCountsUnansweredAsZero_AndFollowUpDoesNotInflateAnsweredCount()
    {
        using var context = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.mf01@fpt.edu.vn",
            FullName = "Lê Vũ Hoàng",
            StudentCode = "SE170999",
            Role = UserRole.Student,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            IsActive = true
        };
        context.Users.Add(student);
        context.Courses.Add(course);

        var qIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        // Phiên 1: Full-Session 5 câu, chỉ mới trả lời 2 câu (8.0đ và 6.0đ) rồi bị đóng do timeout 5 phút
        var fullSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            Course = course,
            PracticeMode = "full_session",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            CompletedAt = DateTime.UtcNow.AddMinutes(-10),
            SelectedDifficulties = PracticeSession.SerializeDifficultiesAndQuestions(
                new[] { "easy", "medium", "hard" },
                qIds)
        };
        var ans1 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = fullSession.Id,
            QuestionId = qIds[0],
            StudentId = student.Id,
            AnswerText = "Trả lời câu 1",
            IsFollowUp = false,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-18)
        };
        ans1.AiEvaluations.Add(new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "practice", PracticeAnswerId = ans1.Id, TotalScore = 8.0m });

        var ans2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = fullSession.Id,
            QuestionId = qIds[1],
            StudentId = student.Id,
            AnswerText = "Trả lời câu 2",
            IsFollowUp = false,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-16)
        };
        ans2.AiEvaluations.Add(new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "practice", PracticeAnswerId = ans2.Id, TotalScore = 6.0m });

        fullSession.Answers.Add(ans1);
        fullSession.Answers.Add(ans2);

        // Phiên 2: Per-Question trả lời 1 câu chính + 2 câu phụ (IsFollowUp = true)
        var perQuestionId = Guid.NewGuid();
        var perQuestionSession = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            Course = course,
            PracticeMode = "per_question",
            Status = "completed",
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-2),
            SelectedDifficulties = PracticeSession.SerializeDifficultiesAndQuestions(
                new[] { "medium" },
                new[] { perQuestionId })
        };
        var mainAns = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = perQuestionSession.Id,
            QuestionId = perQuestionId,
            StudentId = student.Id,
            AnswerText = "Câu trả lời chính",
            IsFollowUp = false,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-8)
        };
        mainAns.AiEvaluations.Add(new AiEvaluation { Id = Guid.NewGuid(), AnswerType = "practice", PracticeAnswerId = mainAns.Id, TotalScore = 6.0m });

        var followUpAns1 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = perQuestionSession.Id,
            QuestionId = perQuestionId,
            ParentAnswerId = mainAns.Id,
            StudentId = student.Id,
            AnswerText = "Câu trả lời phụ 1",
            IsFollowUp = true,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-6)
        };
        var followUpAns2 = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = perQuestionSession.Id,
            QuestionId = perQuestionId,
            ParentAnswerId = mainAns.Id,
            StudentId = student.Id,
            AnswerText = "Câu trả lời phụ 2",
            IsFollowUp = true,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-4)
        };

        perQuestionSession.Answers.Add(mainAns);
        perQuestionSession.Answers.Add(followUpAns1);
        perQuestionSession.Answers.Add(followUpAns2);

        context.PracticeSessions.AddRange(fullSession, perQuestionSession);
        await context.SaveChangesAsync();

        // 1. Kiểm tra Lịch sử Luyện tập (FE-02)
        var historyHandler = new GetStudentPracticeHistoryQueryHandler(context);
        var historyRes = await historyHandler.Handle(new GetStudentPracticeHistoryQuery(student.Id), CancellationToken.None);
        historyRes.IsSuccess.Should().BeTrue();
        historyRes.Value.Should().HaveCount(2);

        var fullSessionItem = historyRes.Value.Single(x => x.SessionId == fullSession.Id);
        fullSessionItem.TotalQuestions.Should().Be(5);
        fullSessionItem.AnsweredQuestions.Should().Be(2);
        // (8.0 + 6.0 + 0 + 0 + 0) / 5 = 2.80
        fullSessionItem.AverageScore.Should().Be(2.80m, "Full-Session đóng tự động phải tính 0đ cho 3 câu chưa làm");

        var perQuestionItem = historyRes.Value.Single(x => x.SessionId == perQuestionSession.Id);
        perQuestionItem.TotalQuestions.Should().Be(1);
        perQuestionItem.AnsweredQuestions.Should().Be(1, "Câu hỏi phụ không được làm AnsweredQuestions vượt quá TotalQuestions");
        perQuestionItem.AnsweredQuestions.Should().BeLessThanOrEqualTo(perQuestionItem.TotalQuestions);

        // 2. Kiểm tra Student Dashboard (FE-10): trung bình của 2 phiên (2.80 và 6.00) là 4.40
        var dashboardHandler = new GetStudentDashboardQueryHandler(context);
        var dashboardRes = await dashboardHandler.Handle(new GetStudentDashboardQuery(student.Id), CancellationToken.None);
        dashboardRes.IsSuccess.Should().BeTrue();
        dashboardRes.Value.AveragePracticeScore.Should().Be(4.40m);
    }

    // =========================================================================
    // 3. [MF02-01] MockExam OrderIndex, Follow-up Duration & Stuck Submission Recovery
    // =========================================================================

    [Fact(DisplayName = "MF02-01: Bảo toàn OrderIndex khi làm Câu 3 trước Câu 1, cộng thêm thời lượng khi cấu trúc > 30p và khôi phục phiên kẹt submitted")]
    public async Task MockExam_PreservesOrderIndexWhenAnsweredOutOfOrder_AndExtendsDurationAbove30Min()
    {
        using var context = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture",
            MaxMockExamsPerDay = 5,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.mf02@fpt.edu.vn",
            FullName = "Nguyễn Trọng Tốt",
            Role = UserRole.Student,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = "Rubric SWD392",
            TotalMaxScore = 10.0m,
            IsActive = true
        };
        rubric.Criteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            CriterionName = "CLO1",
            MaxScore = 10.0m,
            OrderIndex = 1
        });
        var structure = new ExamStructure
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CreatedBy = student.Id,
            Name = "Cấu trúc 60 phút",
            TotalQuestions = 3,
            DurationMinutes = 60, // Cấu trúc gốc 60 phút
            BloomDistribution = "{\"Apply\": 3}",
            IsActive = true
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.ExamStructures.Add(structure);

        for (int i = 1; i <= 3; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Rubric = rubric,
                Title = $"Câu hỏi #{i}",
                Content = $"Nội dung câu hỏi #{i}",
                BloomLevel = "Apply",
                Difficulty = "medium",
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        // 1. Bắt đầu ca thi thử với HasFollowUp = true trên cấu trúc 60 phút -> Phải tăng lên 75 phút (không bị tụt xuống 45 phút)
        var startHandler = new StartMockExamCommandHandler(context);
        var startRes = await startHandler.Handle(new StartMockExamCommand(course.Id, student.Id, HasFollowUp: true), CancellationToken.None);
        startRes.IsSuccess.Should().BeTrue();
        startRes.Value!.DurationMinutes.Should().Be(75, "Khi cấu trúc gốc là 60 phút và bật Follow-up thì thời lượng phải dài hơn (75 phút)");

        var orderedQuestionIds = startRes.Value.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => q.Id)
            .ToList();

        // 2. Sinh viên làm Câu 3 trước, sau đó làm Câu 1 (khiến AnsweredAt của Câu 3 nhỏ hơn Câu 1)
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.NormalizeTranscript(It.IsAny<string>())).Returns<string>(t => t);
        var answerHandler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        await answerHandler.Handle(new SubmitMockExamAnswerCommand(
            startRes.Value.SessionId, student.Id, orderedQuestionIds[2], null, null, null, 20, "Trả lời câu 3 trước"), CancellationToken.None);
        await Task.Delay(15);
        await answerHandler.Handle(new SubmitMockExamAnswerCommand(
            startRes.Value.SessionId, student.Id, orderedQuestionIds[0], null, null, null, 25, "Trả lời câu 1 sau"), CancellationToken.None);

        // Giả lập phiên bị kẹt ở trạng thái "submitted" với TotalScore = null (chưa có câu nào graded)
        var sessionInDb = await context.MockExamSessions.FindAsync(startRes.Value.SessionId);
        sessionInDb!.Status = "submitted";
        sessionInDb.TotalScore = null;
        sessionInDb.SubmittedAt = DateTime.UtcNow.AddMinutes(-2);
        await context.SaveChangesAsync();

        // 3. Nộp lại phiên thi -> Phải khôi phục chấm thành công và giữ nguyên OrderIndex 1, 2, 3 theo đề gốc
        var aiMock = new Mock<IAiGradingService>();
        aiMock.Setup(x => x.GradeAnswerAsync(It.IsAny<AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiGradingResult { Score = 8.0m, ConfidenceScore = 0.9m, Feedback = "Tốt" });

        var submitHandler = new SubmitMockExamSessionCommandHandler(context, aiMock.Object);
        var submitRes = await submitHandler.Handle(new SubmitMockExamSessionCommand(startRes.Value.SessionId, student.Id), CancellationToken.None);

        submitRes.IsSuccess.Should().BeTrue();
        submitRes.Value.Questions.Select(q => q.QuestionId).Should().Equal(orderedQuestionIds);
        submitRes.Value.Questions.Select(q => q.OrderIndex).Should().Equal(1, 2, 3);

        // 4. Tra cứu lại qua GetMockExamSessionDetailQueryHandler cũng giữ nguyên OrderIndex 1, 2, 3
        var detailHandler = new GetMockExamSessionDetailQueryHandler(context);
        var detailRes = await detailHandler.Handle(new GetMockExamSessionDetailQuery(startRes.Value.SessionId, student.Id), CancellationToken.None);
        detailRes.IsSuccess.Should().BeTrue();
        detailRes.Value.Questions.Select(q => q.QuestionId).Should().Equal(orderedQuestionIds);
        detailRes.Value.Questions.Select(q => q.OrderIndex).Should().Equal(1, 2, 3);
    }

    // =========================================================================
    // 4. [RBAC-01] & [RBAC-02] Role Enforcement & ?studentId=... Resolution
    // =========================================================================

    [Fact(DisplayName = "RBAC-01: CoursesController.UpdateConfiguration cấm role lecturer ở cả Attribute, Controller và Handler")]
    public async Task CoursesConfiguration_ForbidsLecturerRole()
    {
        var updateMethod = typeof(CoursesController).GetMethod(nameof(CoursesController.UpdateConfiguration));
        updateMethod.Should().NotBeNull();
        var authAttr = updateMethod!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("department_head,admin");

        var senderMock = new Mock<ISender>();
        var controller = new CoursesController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, "lecturer")
                    }, "TestAuth"))
                }
            }
        };

        var actionResult = await controller.UpdateConfiguration(Guid.NewGuid(), new UpdateCourseConfigRequestDto
        {
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = true
        });

        var forbiddenResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        using var dbContext = CreateDbContext();
        var handler = new UpdateCourseConfigurationCommandHandler(dbContext);
        var handlerRes = await handler.Handle(
            new UpdateCourseConfigurationCommand(Guid.NewGuid(), 60, 2, true) { ActorRole = "lecturer" },
            CancellationToken.None);
        handlerRes.IsSuccess.Should().BeFalse();
        handlerRes.Error.Should().StartWith("FORBIDDEN:");
    }

    [Fact(DisplayName = "RBAC-02: StudentController và MockExamsController ưu tiên ?studentId=... khi caller là Admin/Trưởng BM/Giảng viên")]
    public async Task Controllers_WhenCalledByElevatedRole_ShouldHonorRequestedStudentIdQueryParam()
    {
        var adminId = Guid.NewGuid();
        var targetStudentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "admin")
        }, "TestAuth"));

        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.Is<GetStudentDashboardQuery>(q => q.StudentId == targetStudentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StudentDashboardDto>.Success(new StudentDashboardDto
            {
                StudentId = targetStudentId,
                StudentCode = "SE170123",
                FullName = "Sinh Viên Được Tra Cứu"
            }));

        senderMock
            .Setup(s => s.Send(It.Is<GetMockExamQuotaQuery>(q => q.StudentId == targetStudentId && q.CourseId == courseId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MockExamQuotaDto>.Success(new MockExamQuotaDto(
                courseId, targetStudentId, DateOnly.FromDateTime(DateTime.UtcNow), 1, 2, 3, true)));

        var studentController = new StudentController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = adminPrincipal } }
        };
        var dashRes = await studentController.GetDashboard(targetStudentId, CancellationToken.None);
        dashRes.Should().BeOfType<OkObjectResult>();
        senderMock.Verify(s => s.Send(It.Is<GetStudentDashboardQuery>(q => q.StudentId == targetStudentId), It.IsAny<CancellationToken>()), Times.Once);

        var mockExamsController = new MockExamsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = adminPrincipal } }
        };
        var quotaRes = await mockExamsController.GetQuota(courseId, targetStudentId);
        quotaRes.Should().BeOfType<OkObjectResult>();
        senderMock.Verify(s => s.Send(It.Is<GetMockExamQuotaQuery>(q => q.StudentId == targetStudentId && q.CourseId == courseId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "LOGIC-05/06/08: Validator đọc động MaxMixedPracticeQuestions, NeedsFollowUp tắt ở lượt cũ khi ParentAnswerId=null và AverageScore ghi nhận điểm cải thiện Follow-up")]
    public async Task PracticeValidatorAndFollowUpProgression_RespectsDynamicBounds_AndImprovesAverageScore()
    {
        using var context = CreateDbContext();
        context.SystemConfigs.Add(new SystemConfig
        {
            Key = "MaxMixedPracticeQuestions",
            Value = "12",
            Description = "Cho phép tối đa 12 câu trộn",
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // 1. Validator đọc động MaxMixedPracticeQuestions = 12
        var validator = new StartPracticeSessionCommandValidator(context);
        var validCmd12 = new StartPracticeSessionCommand(Guid.NewGuid(), Guid.NewGuid(), "progressive", 12, true);
        validator.Validate(validCmd12).IsValid.Should().BeTrue("Admin đã nâng MaxMixedPracticeQuestions lên 12 trong system_configs");

        // 2. Khi nộp Follow-up với ParentAnswerId = null, câu gốc phải tắt NeedsFollowUp và điểm cải thiện được tính vào AverageScore
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = ".NET 8 API", IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "sv.followup@fpt.edu.vn", FullName = "SV FollowUp", Role = UserRole.Student, IsActive = true };
        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric", TotalMaxScore = 10.0m, IsActive = true };
        var criterion = new RubricCriterion { Id = Guid.NewGuid(), RubricId = rubric.Id, CriterionName = "Kiến thức", MaxScore = 10.0m, OrderIndex = 1 };
        rubric.Criteria.Add(criterion);
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            RubricId = rubric.Id,
            Title = "Q1",
            Content = "Giải thích DI và IoC Container trong .NET 8?",
            SampleAnswer = "DI tách rời sự phụ thuộc giữa các lớp, còn IoC Container quản lý vòng đời Transient, Scoped và Singleton.",
            Difficulty = "medium",
            BloomLevel = "Understand",
            IsActive = true
        };
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            CourseId = course.Id,
            PracticeMode = "per_question",
            Status = "in_progress",
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            LastActivityAt = DateTime.UtcNow,
            SelectedDifficulties = PracticeSession.SerializeDifficultiesAndQuestions(new[] { "medium" }, new[] { question.Id })
        };

        var mainAnswer = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            StudentId = student.Id,
            AnswerText = "DI giúp giảm phụ thuộc giữa các class.",
            IsFollowUp = false,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-3),
            AiEvaluations = new List<AiEvaluation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AnswerType = "practice",
                    TotalScore = 5.5m,
                    ConfidenceScore = 0.9m,
                    CotTrace = "CoT [FOLLOW_UP_PROMPT]: Hãy phân tích rõ cơ chế quản lý vòng đời của IoC Container?",
                    EvaluatedAt = DateTime.UtcNow.AddMinutes(-3)
                }
            }
        };

        var followUpAnswerWithoutParentId = new PracticeAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = question.Id,
            ParentAnswerId = null, // Giả lập client quên gửi ParentAnswerId
            StudentId = student.Id,
            AnswerText = "IoC Container quản lý 3 vòng đời Transient, Scoped và Singleton.",
            IsFollowUp = true,
            Status = "graded",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-1),
            AiEvaluations = new List<AiEvaluation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AnswerType = "practice",
                    TotalScore = 8.5m,
                    ConfidenceScore = 0.95m,
                    CotTrace = "CoT cải thiện điểm",
                    EvaluatedAt = DateTime.UtcNow.AddMinutes(-1)
                }
            }
        };

        context.Courses.Add(course);
        context.Users.Add(student);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.PracticeSessions.Add(session);
        context.PracticeAnswers.AddRange(mainAnswer, followUpAnswerWithoutParentId);
        await context.SaveChangesAsync();

        var sessionRes = await new GetPracticeSessionQueryHandler(context)
            .Handle(new GetPracticeSessionQuery(session.Id, student.Id), CancellationToken.None);
        sessionRes.IsSuccess.Should().BeTrue();
        var mainDto = sessionRes.Value.Answers.Single(a => a.AnswerId == mainAnswer.Id);
        mainDto.NeedsFollowUp.Should().BeFalse("Khi đã có lượt trả lời phụ mới hơn cho cùng QuestionId thì câu gốc phải tắt NeedsFollowUp kể cả khi ParentAnswerId=null");

        var historyRes = await new GetStudentPracticeHistoryQueryHandler(context)
            .Handle(new GetStudentPracticeHistoryQuery(student.Id), CancellationToken.None);
        historyRes.IsSuccess.Should().BeTrue();
        historyRes.Value.Single().AverageScore.Should().Be(8.50m, "AverageScore phải ghi nhận điểm cải thiện 8.50 sau khi trả lời Follow-up");
    }
}
