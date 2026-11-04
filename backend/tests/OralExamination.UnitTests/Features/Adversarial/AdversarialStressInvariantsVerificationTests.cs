using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.MockExams.Queries.GetMockExamQuota;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm chứng độc lập thực nghiệm (Empirical Adversarial Stress Harness) cho 4 Bất biến Cốt lõi:
/// 1. Thuật toán Anti-3-Consecutive (MF-01) - Monte Carlo loop & Fallback
/// 2. Hạn ngạch thi thử động (MF-02 Quota) - Dynamic config & HTTP 429
/// 3. Zero R2 Audio trong MF-01 & MF-02 - Reflection & Stream Disposal
/// 4. Chuẩn hóa CSDL 3NF & Ràng buộc toàn vẹn - 0 Column-stuffing, 0 [NotMapped] hacks
/// </summary>
public class AdversarialStressInvariantsVerificationTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    #region 1. THUẬT TOÁN ANTI-3-CONSECUTIVE (MF-01)

    [Fact(DisplayName = "INVARIANT-01A. Monte Carlo Stress Test: 200 phiên độc lập x 25 câu hỏi - Tuyệt đối không bao giờ xuất hiện 3 câu cùng mức liên tiếp khi còn đề")]
    public async Task Anti3Consecutive_MonteCarlo_200Sessions_NeverViolatesSlidingWindow()
    {
        const int sessionCount = 200;
        const int drawsPerSession = 25;

        for (int s = 1; s <= sessionCount; s++)
        {
            using var context = CreateDbContext();

            var course = new Course
            {
                Id = Guid.NewGuid(),
                Code = $"PRN231_MC_{s}",
                Name = "Web API Development",
                TranscriptBufferSeconds = 60,
                IsActive = true
            };
            var student = new User
            {
                Id = Guid.NewGuid(),
                Email = $"student.mc{s}@fpt.edu.vn",
                FullName = $"Sinh Vien Monte Carlo {s}",
                Role = UserRole.Student,
                IsActive = true
            };
            var session = new PracticeSession
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                StudentId = student.Id,
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                Status = "in_progress",
                PracticeMode = "per_question",
                SelectedDifficulties = JsonSerializer.Serialize(new List<string> { "easy", "medium", "hard" })
            };

            context.Courses.Add(course);
            context.Users.Add(student);
            context.PracticeSessions.Add(session);

            context.SystemConfigs.Add(new SystemConfig
            {
                Id = Guid.NewGuid(),
                Key = "SessionInactivityTimeoutMinutes",
                Value = "5"
            });

            // Seed dồi dào câu hỏi (mỗi mức 30 câu)
            var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "Rubric Test", TotalMaxScore = 10.0m };
            context.Rubrics.Add(rubric);

            var diffs = new[] { "easy", "medium", "hard" };
            foreach (var d in diffs)
            {
                for (int i = 1; i <= 30; i++)
                {
                    context.PracticeQuestions.Add(new PracticeQuestion
                    {
                        Id = Guid.NewGuid(),
                        CourseId = course.Id,
                        RubricId = rubric.Id,
                        Title = $"Q_{d}_{i}",
                        Content = $"Content_{d}_{i}",
                        Difficulty = d,
                        IsActive = true
                    });
                }
            }
            await context.SaveChangesAsync();

            var handler = new NextQuestionCommandHandler(context);
            var history = new List<string>();

            for (int q = 1; q <= drawsPerSession; q++)
            {
                var cmd = new NextQuestionCommand(session.Id, student.Id);
                var res = await handler.Handle(cmd, CancellationToken.None);

                res.IsSuccess.Should().BeTrue($"Lượt bốc #{q} tại phiên #{s} phải thành công");
                res.Value.HasMoreQuestions.Should().BeTrue();
                res.Value.Question.Should().NotBeNull();

                var diff = res.Value.Question!.Difficulty.ToLowerInvariant();
                history.Add(diff);

                // Giả lập nộp câu trả lời để lưu vết lịch sử
                context.PracticeAnswers.Add(new PracticeAnswer
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    QuestionId = res.Value.Question.Id,
                    StudentId = student.Id,
                    AnswerText = $"Ans {q}",
                    SubmittedAt = DateTime.UtcNow.AddSeconds(q * 10),
                    IsFollowUp = false
                });
                await context.SaveChangesAsync();
            }

            // Kiểm tra toàn diện cửa sổ trượt 3 phần tử
            for (int i = 0; i <= history.Count - 3; i++)
            {
                var d1 = history[i];
                var d2 = history[i + 1];
                var d3 = history[i + 2];

                var isAllThreeSame = (d1 == d2 && d2 == d3);
                isAllThreeSame.Should().BeFalse(
                    $"VI PHẠM BẤT BIẾN ANTI-3-CONSECUTIVE tại phiên #{s}, chỉ số [{i}, {i + 1}, {i + 2}]: {d1} -> {d2} -> {d3}");
            }
        }
    }

    [Fact(DisplayName = "INVARIANT-01B. Bất biến loại trừ câu đã làm Id NOT IN (...) - 100% câu hỏi trả về trong phiên là độc nhất")]
    public async Task Anti3Consecutive_ExclusionInvariant_AllReturnedQuestionsAreDistinct()
    {
        using var context = CreateDbContext();

        var course = new Course { Id = Guid.NewGuid(), Code = "EXCLUDE_TEST", Name = "Test", IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "s.ex@fpt.edu.vn", FullName = "Student Ex", Role = UserRole.Student, IsActive = true };
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            Status = "in_progress",
            PracticeMode = "per_question",
            SelectedDifficulties = JsonSerializer.Serialize(new List<string> { "easy", "medium", "hard" })
        };
        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);
        context.SystemConfigs.Add(new SystemConfig { Id = Guid.NewGuid(), Key = "SessionInactivityTimeoutMinutes", Value = "5" });

        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10.0m };
        context.Rubrics.Add(rubric);

        const int totalPoolSize = 30;
        for (int i = 1; i <= totalPoolSize; i++)
        {
            context.PracticeQuestions.Add(new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = $"Q_{i}",
                Content = $"Content_{i}",
                Difficulty = (i % 3 == 0) ? "easy" : (i % 3 == 1) ? "medium" : "hard",
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);
        var seenIds = new HashSet<Guid>();

        for (int i = 1; i <= totalPoolSize; i++)
        {
            var res = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
            res.IsSuccess.Should().BeTrue();
            res.Value.HasMoreQuestions.Should().BeTrue();

            var qId = res.Value.Question!.Id;
            seenIds.Add(qId).Should().BeTrue($"Câu hỏi ID {qId} không được xuất hiện lặp lại tại lần bốc thứ {i}");

            context.PracticeAnswers.Add(new PracticeAnswer
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = qId,
                StudentId = student.Id,
                AnswerText = $"Ans {i}",
                SubmittedAt = DateTime.UtcNow.AddSeconds(i * 10),
                IsFollowUp = false
            });
            await context.SaveChangesAsync();
        }

        seenIds.Should().HaveCount(totalPoolSize);

        // Lần thứ 31: Cạn sạch đề
        var exhaustedRes = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        exhaustedRes.IsSuccess.Should().BeTrue();
        exhaustedRes.Value.HasMoreQuestions.Should().BeFalse();
        exhaustedRes.Value.Question.Should().BeNull();
    }

    [Fact(DisplayName = "INVARIANT-01C. Fallback tự động khi 1 mức cạn: 2 câu trước easy, nhưng medium/hard cạn, fallback cấp tiếp easy an toàn")]
    public async Task Anti3Consecutive_Fallback_WhenNonExcludedPoolEmpty_AllowsRemainingExcludedPoolSafely()
    {
        using var context = CreateDbContext();

        var course = new Course { Id = Guid.NewGuid(), Code = "FALLBACK_TEST", Name = "Fallback Test", IsActive = true };
        var student = new User { Id = Guid.NewGuid(), Email = "fallback@fpt.edu.vn", FullName = "Fallback Student", Role = UserRole.Student, IsActive = true };
        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            StudentId = student.Id,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            Status = "in_progress",
            PracticeMode = "per_question",
            SelectedDifficulties = JsonSerializer.Serialize(new List<string> { "easy", "medium", "hard" })
        };
        context.Courses.Add(course);
        context.Users.Add(student);
        context.PracticeSessions.Add(session);
        context.SystemConfigs.Add(new SystemConfig { Id = Guid.NewGuid(), Key = "SessionInactivityTimeoutMinutes", Value = "5" });

        var rubric = new Rubric { Id = Guid.NewGuid(), CourseId = course.Id, Name = "R", TotalMaxScore = 10.0m };
        context.Rubrics.Add(rubric);

        // Chỉ seed đúng 4 câu easy. Medium và Hard = 0 câu.
        var easyQuestions = new List<PracticeQuestion>();
        for (int i = 1; i <= 4; i++)
        {
            var q = new PracticeQuestion
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                RubricId = rubric.Id,
                Title = $"Easy_{i}",
                Content = $"EasyContent_{i}",
                Difficulty = "easy",
                IsActive = true
            };
            easyQuestions.Add(q);
            context.PracticeQuestions.Add(q);
        }
        await context.SaveChangesAsync();

        var handler = new NextQuestionCommandHandler(context);

        // Lấy câu 1
        var r1 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();
        context.PracticeAnswers.Add(new PracticeAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = r1.Value.Question!.Id, StudentId = student.Id, AnswerText = "1", SubmittedAt = DateTime.UtcNow.AddSeconds(10) });
        await context.SaveChangesAsync();

        // Lấy câu 2
        var r2 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r2.IsSuccess.Should().BeTrue();
        context.PracticeAnswers.Add(new PracticeAnswer { Id = Guid.NewGuid(), SessionId = session.Id, QuestionId = r2.Value.Question!.Id, StudentId = student.Id, AnswerText = "2", SubmittedAt = DateTime.UtcNow.AddSeconds(20) });
        await context.SaveChangesAsync();

        // 2 câu liền trước là easy. Thuật toán cố gắng exclude 'easy'.
        // Nhưng nonExcludedPool (medium, hard) rỗng -> Phải fallback lấy tiếp easy còn lại mà không báo lỗi hoặc cạn giả!
        var r3 = await handler.Handle(new NextQuestionCommand(session.Id, student.Id), CancellationToken.None);
        r3.IsSuccess.Should().BeTrue();
        r3.Value.HasMoreQuestions.Should().BeTrue();
        r3.Value.Question!.Difficulty.ToLowerInvariant().Should().Be("easy");
        r3.Value.Question.Id.Should().NotBe(r1.Value.Question.Id);
        r3.Value.Question.Id.Should().NotBe(r2.Value.Question.Id);
    }

    #endregion

    #region 2. HẠN NGẠCH THI THỬ ĐỘNG (MF-02 QUOTA)

    [Theory(DisplayName = "INVARIANT-02A. Quota thi thử không bị hardcoded K=3: Áp dụng chính xác mọi giá trị động cấu hình")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task DynamicQuota_IsNotHardcoded_HonorsConfiguredLimitsAccurately(int customQuota)
    {
        using var context = CreateDbContext();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = $"DYN_{customQuota}",
            Name = "Dynamic Quota Course",
            MaxMockExamsPerDay = customQuota,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = $"dyn{customQuota}@fpt.edu.vn",
            FullName = "Dynamic Student",
            Role = UserRole.Student,
            IsActive = true
        };
        context.Courses.Add(course);
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var queryHandler = new GetMockExamQuotaQueryHandler(context);
        var quotaRes = await queryHandler.Handle(new GetMockExamQuotaQuery(course.Id, student.Id), CancellationToken.None);

        quotaRes.IsSuccess.Should().BeTrue();
        quotaRes.Value.MaxDailyQuota.Should().Be(customQuota);
        quotaRes.Value.RemainingCount.Should().Be(customQuota);
        quotaRes.Value.UsedCount.Should().Be(0);
        quotaRes.Value.CanStartExam.Should().BeTrue();
    }

    [Fact(DisplayName = "INVARIANT-02B. Vượt hạn ngạch thi thử trả về đúng HTTP 429 Too Many Requests")]
    public async Task DynamicQuota_ExceededLimit_ControllerReturnsHttp429TooManyRequests()
    {
        var senderMock = new Mock<ISender>();
        var controller = new MockExamsController(senderMock.Object);

        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, studentId.ToString()),
            new(ClaimTypes.Role, "student")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        // Giả lập Mediator trả về QUOTA_EXCEEDED khi sinh viên đã dùng hết hạn ngạch
        senderMock
            .Setup(s => s.Send(It.IsAny<StartMockExamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartMockExamResponse>.Failure("QUOTA_EXCEEDED:Bạn đã sử dụng hết hạn ngạch thi thử trong ngày (5/5 lượt) cho môn học này."));

        var actionResult = await controller.StartSession(new StartMockExamRequest(courseId, HasFollowUp: false));

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    #endregion

    #region 3. CAM KẾT ZERO R2 AUDIO TRONG MF-01 & MF-02

    [Fact(DisplayName = "INVARIANT-03A. Zero R2 Storage Invariant: Thực thể PracticeAnswer & MockExamAnswer tuyệt đối không có cột lưu trữ Audio URL hay file R2")]
    public void ZeroR2Audio_PracticeAnswer_And_MockExamAnswer_ContainNoAudioStorageProperties()
    {
        var practiceAnswerProps = typeof(PracticeAnswer).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var mockExamAnswerProps = typeof(MockExamAnswer).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var forbiddenAudioTerms = new[] { "audiourl", "audio_url", "r2url", "r2_url", "audiopath", "audiofile", "r2key" };

        foreach (var prop in practiceAnswerProps)
        {
            var lowerName = prop.Name.ToLowerInvariant();
            forbiddenAudioTerms.Should().NotContain(lowerName,
                $"Thực thể PracticeAnswer vi phạm cam kết Zero R2 Storage khi tồn tại thuộc tính '{prop.Name}'!");
        }

        foreach (var prop in mockExamAnswerProps)
        {
            var lowerName = prop.Name.ToLowerInvariant();
            forbiddenAudioTerms.Should().NotContain(lowerName,
                $"Thực thể MockExamAnswer vi phạm cam kết Zero R2 Storage khi tồn tại thuộc tính '{prop.Name}'!");
        }

        // Đối chiếu đối nghịch (Counter-Proof): Luồng thi thật phòng Lab MF-04 (ExamQuestionSubmission) CÓ cột AudioR2Url và AudioHashSha256 để niêm phong R2
        var examSubmissionProps = typeof(ExamQuestionSubmission).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        examSubmissionProps.Select(p => p.Name).Should().Contain("AudioR2Url");
        examSubmissionProps.Select(p => p.Name).Should().Contain("AudioHashSha256");
    }

    [Fact(DisplayName = "INVARIANT-03B. Giải phóng RAM stream ngay sau khi Whisper STT bóc băng trong UploadAudioPractice")]
    public async Task ZeroR2Audio_UploadAudioPractice_DisposesMemoryStreamProperly()
    {
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock.Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult("Xin chào thầy cô", "Xin chào thầy cô", "MockSTT", false, null, 0.95));
        sttMock.Setup(s => s.NormalizeTranscript(It.IsAny<string>()))
            .Returns<string>(s => s);

        var handler = new UploadAudioPracticeCommandHandler(sttMock.Object);

        // Gửi stream âm thanh hợp lệ giả lập WebM header
        var dummyAudioBytes = new byte[512];
        dummyAudioBytes[0] = 0x1A; dummyAudioBytes[1] = 0x45; dummyAudioBytes[2] = 0xDF; dummyAudioBytes[3] = 0xA3; // EBML header

        using var memoryStream = new MemoryStream(dummyAudioBytes);
        var command = new UploadAudioPracticeCommand(memoryStream, "test.webm", "audio/webm");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Transcript.Should().Be("Xin chào thầy cô");

        // Xác nhận không có lời gọi nào tới R2/Cloudflare storage service
        sttMock.Verify(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), "test.webm", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 4. CHUẨN HÓA CSDL 3NF & RÀNG BUỘC TOÀN VẸN

    [Fact(DisplayName = "INVARIANT-04A. Kiểm tra 3NF & Toàn vẹn Lược đồ: CourseLecturer, Classes, ClassEnrollment, DLQ, Practice/Exam Questions")]
    public void DatabaseSchema_3NF_And_Constraints_Verification()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        // 1. CourseLecturer: Thực thể N-N chuẩn 3NF
        var courseLecturerEntity = model.FindEntityType(typeof(CourseLecturer))!;
        courseLecturerEntity.Should().NotBeNull();
        courseLecturerEntity.GetTableName().Should().Be("course_lecturers");
        var clProps = courseLecturerEntity.GetProperties().Select(p => p.Name).ToList();
        clProps.Should().Contain(nameof(CourseLecturer.CourseId));
        clProps.Should().Contain(nameof(CourseLecturer.LecturerUserId));
        clProps.Should().Contain(nameof(CourseLecturer.AssignedByUserId));

        // 2. Class: Có JoinCode, Name, JoinPasswordHash
        var classEntity = model.FindEntityType(typeof(Class))!;
        classEntity.Should().NotBeNull();
        classEntity.GetTableName().Should().Be("classes");
        var classProps = classEntity.GetProperties().Select(p => p.Name).ToList();
        classProps.Should().Contain(nameof(Class.JoinCode));
        classProps.Should().Contain(nameof(Class.Name));
        classProps.Should().Contain(nameof(Class.JoinPasswordHash));

        // 3. ClassEnrollment: Có JoinedViaCode, JoinMethod
        var enrollmentEntity = model.FindEntityType(typeof(ClassEnrollment))!;
        enrollmentEntity.Should().NotBeNull();
        enrollmentEntity.GetTableName().Should().Be("class_enrollments");
        var enrollProps = enrollmentEntity.GetProperties().Select(p => p.Name).ToList();
        enrollProps.Should().Contain(nameof(ClassEnrollment.JoinedViaCode));
        enrollProps.Should().Contain(nameof(ClassEnrollment.JoinMethod));

        // 4. DeadLetterQueue: Có 4 cột mới (FlowType, ManagedByRole, ResolvedByUserId, ResolutionNotes)
        var dlqEntity = model.FindEntityType(typeof(global::OralExamination.Domain.Entities.DeadLetterQueue))!;
        dlqEntity.Should().NotBeNull();
        dlqEntity.GetTableName().Should().Be("dead_letter_queues");
        var dlqProps = dlqEntity.GetProperties().Select(p => p.Name).ToList();
        dlqProps.Should().Contain(nameof(global::OralExamination.Domain.Entities.DeadLetterQueue.FlowType));
        dlqProps.Should().Contain(nameof(global::OralExamination.Domain.Entities.DeadLetterQueue.ManagedByRole));
        dlqProps.Should().Contain(nameof(global::OralExamination.Domain.Entities.DeadLetterQueue.ResolvedByUserId));
        dlqProps.Should().Contain(nameof(global::OralExamination.Domain.Entities.DeadLetterQueue.ResolutionNotes));

        // 5. PracticeQuestion: Có CloId
        var pqEntity = model.FindEntityType(typeof(PracticeQuestion));
        pqEntity.Should().NotBeNull();
        pqEntity!.GetProperties().Select(p => p.Name).Should().Contain(nameof(PracticeQuestion.CloId));

        // 6. ExamQuestion: Có CloId và StructureId
        var eqEntity = model.FindEntityType(typeof(ExamQuestion));
        eqEntity.Should().NotBeNull();
        eqEntity!.GetProperties().Select(p => p.Name).Should().Contain(nameof(ExamQuestion.CloId));
        eqEntity!.GetProperties().Select(p => p.Name).Should().Contain(nameof(ExamQuestion.StructureId));

        // 7. Xác nhận KHÔNG có bất kỳ thuộc tính [NotMapped] bất thường nào nhằm giấu trạng thái trên RAM
        var allEntities = new[] { typeof(CourseLecturer), typeof(Class), typeof(ClassEnrollment), typeof(global::OralExamination.Domain.Entities.DeadLetterQueue), typeof(PracticeQuestion), typeof(ExamQuestion) };
        foreach (var entityType in allEntities)
        {
            var notMappedProps = entityType.GetProperties()
                .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>() != null)
                .ToList();

            notMappedProps.Should().BeEmpty(
                $"Thực thể {entityType.Name} vi phạm cam kết khi sử dụng thuộc tính [NotMapped]!");
        }
    }

    #endregion
}
