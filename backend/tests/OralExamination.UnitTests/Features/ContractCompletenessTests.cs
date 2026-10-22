using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamStructure;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamStructure;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamStructures;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateDraftQuestion;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetFlmSyllabus;
using OralExamination.Application.Features.QuestionBank.Queries.GetRubrics;
using OralExamination.Application.Features.Student.Queries.GetOfficialExamGrade;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features;

public class ContractCompletenessTests
{
    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"ContractCompleteness_{Guid.NewGuid()}")
            .Options;
        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. AuditEvidenceItemDto.CotTrace được serialize đồng thời cùng AiChainOfThought cho Frontend Evidence Panel")]
    public void AuditEvidenceItemDto_Should_Serialize_Both_AiChainOfThought_And_CotTrace()
    {
        var dto = new AuditEvidenceItemDto
        {
            TicketId = Guid.NewGuid(),
            StudentCode = "SE170123",
            FullName = "Nguyễn Văn A",
            SeatNumber = 1,
            Status = ExamTicketStatus.AiGraded,
            AiChainOfThought = "Bước 1: Phân tích transcript. Bước 2: Đối chiếu Rubric 10.0đ."
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("aiChainOfThought", out var aiCot).Should().BeTrue();
        doc.RootElement.TryGetProperty("cotTrace", out var cotTrace).Should().BeTrue();
        aiCot.GetString().Should().Be("Bước 1: Phân tích transcript. Bước 2: Đối chiếu Rubric 10.0đ.");
        cotTrace.GetString().Should().Be("Bước 1: Phân tích transcript. Bước 2: Đối chiếu Rubric 10.0đ.");
    }

    [Fact(DisplayName = "2. GetFlmSyllabusQueryHandler trả về đề cương FLM, hỗ trợ lọc CLO và từ khóa chủ đề")]
    public async Task GetFlmSyllabusQueryHandler_Should_Return_Filtered_Syllabus_And_404_When_Course_Missing()
    {
        await using var db = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var handler = new GetFlmSyllabusQueryHandler(db);

        var missingResult = await handler.Handle(new GetFlmSyllabusQuery(Guid.NewGuid()), CancellationToken.None);
        missingResult.IsSuccess.Should().BeFalse();
        missingResult.Error.Should().StartWith("NOT_FOUND:");

        var fullResult = await handler.Handle(new GetFlmSyllabusQuery(course.Id, 1, 10, "Clean Architecture", "CLO2"), CancellationToken.None);
        fullResult.IsSuccess.Should().BeTrue();
        fullResult.Value.CourseCode.Should().Be("PRN231");
        fullResult.Value.Clos.Should().HaveCountGreaterOrEqualTo(4);
        fullResult.Value.Topics.Should().NotBeEmpty();
        fullResult.Value.Topics.Should().OnlyContain(t =>
            t.TopicName.Contains("Clean Architecture", StringComparison.OrdinalIgnoreCase) &&
            t.MappedClos.Contains("CLO2"));
    }

    [Fact(DisplayName = "3. CreateRubricCommandHandler khóa cứng tổng điểm 10.00đ và GetRubricsQueryHandler trả về danh sách Barem")]
    public async Task CreateRubric_And_GetRubrics_Should_Enforce_10Point_Invariant()
    {
        await using var db = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWP391",
            Name = "Software Development Project",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var createHandler = new CreateRubricCommandHandler(db);

        // Sai tổng điểm (9.00 != 10.00)
        var invalidResult = await createHandler.Handle(new CreateRubricCommand(
            Guid.NewGuid(),
            course.Id,
            "Barem lỗi",
            "Tổng điểm lệch",
            new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí 1", 5.00m, 0.5m, "Understand", "Mô tả 1", 1),
                new("Tiêu chí 2", 4.00m, 0.5m, "Apply", "Mô tả 2", 2)
            }), CancellationToken.None);

        invalidResult.IsSuccess.Should().BeFalse();
        invalidResult.Error.Should().StartWith("INVARIANT:");

        // Đúng chuẩn 10.00 điểm
        var validResult = await createHandler.Handle(new CreateRubricCommand(
            Guid.NewGuid(),
            course.Id,
            "Barem chuẩn 10đ",
            "Đánh giá kiến trúc",
            new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí 1", 4.00m, 0.4m, "Understand", "Mô tả 1", 1),
                new("Tiêu chí 2", 6.00m, 0.6m, "Analyze", "Mô tả 2", 2)
            }), CancellationToken.None);

        validResult.IsSuccess.Should().BeTrue();
        validResult.Value.TotalMaxScore.Should().Be(10.00m);
        validResult.Value.CriteriaCount.Should().Be(2);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "CREATE_RUBRIC" && a.EntityId == validResult.Value.Id);
        auditLog.Should().NotBeNull("CreateRubricCommandHandler phải ghi AuditLog khi ActorUserId khác Guid.Empty");
        auditLog!.EntityName.Should().Be("rubrics");
        auditLog.NewValues.Should().Be("Barem chuẩn 10đ");

        var getHandler = new GetRubricsQueryHandler(db);
        var rubricsResult = await getHandler.Handle(new GetRubricsQuery(course.Id), CancellationToken.None);
        rubricsResult.IsSuccess.Should().BeTrue();
        rubricsResult.Value.Should().ContainSingle(r => r.Id == validResult.Value.Id);

        // Tạo câu hỏi liên kết trực tiếp với RubricId vừa tạo
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "GV Test",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        db.Users.Add(lecturer);
        await db.SaveChangesAsync();

        var draftQuestionHandler = new CreateDraftQuestionCommandHandler(db);
        var questionResult = await draftQuestionHandler.Handle(new CreateDraftQuestionCommand(
            course.Id,
            lecturer.Id,
            "Câu hỏi gắn RubricId có sẵn",
            "Trình bày nguyên lý SOLID trong thiết kế hướng đối tượng.",
            new string('A', 60),
            new List<string> { "SRP", "DIP" },
            "medium",
            "Understand",
            "manual",
            "practice",
            true,
            "Câu hỏi phụ",
            null!,
            validResult.Value.Id,
            null), CancellationToken.None);

        questionResult.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "4. BatchApproveQuestionsCommandHandler kiểm tra quyền Trưởng BM, Barem 10.0đ và lưu đồng thời 2 kho khi shared")]
    public async Task BatchApproveQuestionsCommandHandler_Should_Validate_And_Save_Shared_Questions()
    {
        await using var db = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Back-End .NET",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = "head@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        db.Courses.Add(course);
        db.Users.Add(head);
        await db.SaveChangesAsync();

        var handler = new BatchApproveQuestionsCommandHandler(db);
        var rubric = new RubricDraftDto(
            "Barem CLO1",
            "Mô tả",
            10.00m,
            new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí A", 5.00m, 0.5m, "Understand", "Mô tả A", 1),
                new("Tiêu chí B", 5.00m, 0.5m, "Analyze", "Mô tả B", 2)
            });

        var result = await handler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            new List<ApprovedQuestionItemDto>
            {
                new(
                    "Phân tích Clean Architecture",
                    "Giải thích chi tiết sự tách biệt giữa Domain và Infrastructure.",
                    "medium",
                    "Analyze",
                    "shared",
                    "flm_api",
                    "Đây là câu trả lời mẫu chuẩn mực có độ dài lớn hơn 50 ký tự để vượt qua kiểm tra invariant.",
                    new List<string> { "Domain độc lập", "DIP" },
                    true,
                    "Nếu đổi DB thì sao?",
                    rubric)
            }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalApproved.Should().Be(1);
        (await db.ExamQuestions.CountAsync()).Should().Be(1);
        (await db.PracticeQuestions.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "5. ExamStructure, OfficialExamSessions, ExamShifts và GetOfficialExamGrade hoạt động đồng bộ")]
    public async Task ExamStructures_Sessions_Shifts_And_StudentOfficialGrade_Should_Work_EndToEnd()
    {
        await using var db = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Back-End .NET",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = "head@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@fpt.edu.vn",
            FullName = "Lê Vũ Hoàng",
            StudentCode = "SE170001",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Courses.Add(course);
        db.Users.AddRange(head, student);
        await db.SaveChangesAsync();

        var createStructureHandler = new CreateExamStructureCommandHandler(db);
        var createdStructure = await createStructureHandler.Handle(new CreateExamStructureCommand(
            head.Id,
            course.Id,
            "Cấu trúc đề thi chuẩn FA26",
            5,
            30,
            "{\"Understand\":2,\"Apply\":2,\"Analyze\":1}",
            true), CancellationToken.None);

        createdStructure.IsSuccess.Should().BeTrue();

        var updateStructureHandler = new UpdateExamStructureCommandHandler(db);
        var updatedStructure = await updateStructureHandler.Handle(new UpdateExamStructureCommand(
            head.Id,
            createdStructure.Value.Id,
            "Cấu trúc đề thi cập nhật FA26",
            6,
            45,
            "{\"Understand\":2,\"Apply\":2,\"Analyze\":2}",
            true), CancellationToken.None);

        updatedStructure.IsSuccess.Should().BeTrue();
        updatedStructure.Value.TotalQuestions.Should().Be(6);
        updatedStructure.Value.DurationMinutes.Should().Be(45);

        var getStructuresHandler = new GetExamStructuresQueryHandler(db);
        var structures = await getStructuresHandler.Handle(new GetExamStructuresQuery(course.Id), CancellationToken.None);
        structures.IsSuccess.Should().BeTrue();
        structures.Value.Should().ContainSingle(s => s.Id == createdStructure.Value.Id);

        // Seed Session + Shift + Published Ticket
        var session = new OfficialExamSession
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            ExamStructureId = createdStructure.Value.Id,
            Title = "Kỳ thi Cuối kỳ FA26",
            ExamDate = new DateOnly(2026, 11, 15),
            HasFollowUp = true,
            MaxFollowUpQuestions = 2,
            ExamInputMode = ExamInputMode.VoiceOnly,
            TranscriptBufferSeconds = 60,
            Status = "scheduled"
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1 Sáng",
            RoomLab = "LAB-302",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "published"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            IpAddress = "10.0.0.11",
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            StudentAcknowledgementStatus = "PENDING"
        };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 8.50m,
            FinalScore = 8.50m,
            GradingStatus = "completed",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-90)
        };
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        db.ExamQuestionSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var sessionsHandler = new GetOfficialExamSessionsQueryHandler(db);
        var sessionsRes = await sessionsHandler.Handle(new GetOfficialExamSessionsQuery(course.Id), CancellationToken.None);
        sessionsRes.IsSuccess.Should().BeTrue();
        sessionsRes.Value.Should().ContainSingle(s => s.Id == session.Id);

        var shiftsHandler = new GetExamShiftsQueryHandler(db);
        var shiftsRes = await shiftsHandler.Handle(new GetExamShiftsQuery(session.Id), CancellationToken.None);
        shiftsRes.IsSuccess.Should().BeTrue();
        shiftsRes.Value.Should().ContainSingle(s => s.Id == shift.Id);

        var gradeHandler = new GetOfficialExamGradeQueryHandler(db);
        var gradeRes = await gradeHandler.Handle(new GetOfficialExamGradeQuery(ticket.Id, student.Id), CancellationToken.None);
        gradeRes.IsSuccess.Should().BeTrue();
        gradeRes.Value.TotalScore.Should().Be(8.50m);
        gradeRes.Value.IsLocked.Should().BeTrue();
        gradeRes.Value.InternalAppeal.CanAppeal.Should().BeTrue();
    }

    [Fact(DisplayName = "6. GetOfficialExamGradeQueryHandler chặn truy cập trái phép (BOLA 403) và vé chưa công bố điểm (422)")]
    public async Task GetOfficialExamGradeQueryHandler_Should_Reject_Foreign_Student_And_Unpublished_Ticket()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = ownerId,
            SeatNumber = 2,
            IpAddress = "10.0.0.12",
            Status = ExamTicketStatus.Submitted,
            IsLocked = false
        };
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new GetOfficialExamGradeQueryHandler(db);

        var bolaRes = await handler.Handle(new GetOfficialExamGradeQuery(ticket.Id, otherStudentId), CancellationToken.None);
        bolaRes.IsSuccess.Should().BeFalse();
        bolaRes.Error.Should().StartWith("FORBIDDEN:");

        var unpublishedRes = await handler.Handle(new GetOfficialExamGradeQuery(ticket.Id, ownerId), CancellationToken.None);
        unpublishedRes.IsSuccess.Should().BeFalse();
        unpublishedRes.Error.Should().StartWith("UNPROCESSABLE:");
    }

    [Fact(DisplayName = "7. BatchApproveQuestionsCommandHandler từ chối khi người duyệt không phải Trưởng BM/Admin hoặc Barem lệch 10.0đ")]
    public async Task BatchApproveQuestionsCommandHandler_Should_Reject_Unauthorized_User_Or_Invalid_Rubric()
    {
        await using var db = CreateDbContext();
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Back-End .NET",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "gv@fpt.edu.vn",
            FullName = "Giảng Viên",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = "bm@fpt.edu.vn",
            FullName = "Trưởng BM",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        db.Courses.Add(course);
        db.Users.AddRange(lecturer, head);
        await db.SaveChangesAsync();

        var handler = new BatchApproveQuestionsCommandHandler(db);

        var forbiddenRes = await handler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            lecturer.Id,
            new List<ApprovedQuestionItemDto>()), CancellationToken.None);
        forbiddenRes.IsSuccess.Should().BeFalse();
        forbiddenRes.Error.Should().StartWith("FORBIDDEN:");

        var invalidRubric = new RubricDraftDto(
            "Barem sai tổng điểm",
            null,
            9.50m,
            new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí 1", 4.50m, 0.5m, "Understand", "Mô tả 1", 1),
                new("Tiêu chí 2", 5.00m, 0.5m, "Apply", "Mô tả 2", 2)
            });

        var invariantRes = await handler.Handle(new BatchApproveQuestionsCommand(
            course.Id,
            head.Id,
            new List<ApprovedQuestionItemDto>
            {
                new(
                    "Câu hỏi lỗi Barem",
                    "Nội dung câu hỏi",
                    "easy",
                    "Understand",
                    "exam",
                    "manual",
                    new string('M', 60),
                    new List<string> { "Ý 1" },
                    false,
                    null,
                    invalidRubric)
            }), CancellationToken.None);
        invariantRes.IsSuccess.Should().BeFalse();
        invariantRes.Error.Should().StartWith("INVARIANT:");
    }

    [Fact(DisplayName = "8. AcknowledgeOfficialGradeResponseDto trả đồng thời AcknowledgedAt và AcknowledgedAtUtc")]
    public void AcknowledgeOfficialGradeResponseDto_Should_Expose_AcknowledgedAt_Alias()
    {
        var now = DateTime.UtcNow;
        var dto = new OralExamination.Application.Features.Student.DTOs.AcknowledgeOfficialGradeResponseDto
        {
            TicketId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            StudentAcknowledgementStatus = "ACKNOWLEDGED",
            AcknowledgedAtUtc = now,
            Message = "Đã xác nhận"
        };

        dto.AcknowledgedAt.Should().Be(now);
    }

    [Fact(DisplayName = "9. GlobalExceptionMiddleware che giấu chi tiết lỗi 500 trên Production nhưng vẫn giữ thông báo lỗi nghiệp vụ 400/403/429")]
    public async Task GlobalExceptionMiddleware_Should_Mask_500_In_Production_And_Preserve_Domain_Errors()
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OralExamination.API.Middlewares.GlobalExceptionMiddleware>.Instance;
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production };
        var devEnv = new FakeHostEnvironment { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development };

        // 1. Lỗi 500 ở Production -> không lộ chuỗi kết nối nội bộ
        var prodContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        prodContext.Response.Body = new System.IO.MemoryStream();
        var prodMiddleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ => throw new Exception("NpgsqlException: Host=10.0.0.5;Password=SuperSecretPassword"),
            logger,
            prodEnv);

        await prodMiddleware.InvokeAsync(prodContext);
        prodContext.Response.StatusCode.Should().Be(500);
        prodContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var prodBody = await new System.IO.StreamReader(prodContext.Response.Body).ReadToEndAsync();
        using var prodDoc = JsonDocument.Parse(prodBody);
        var prodDetail = prodDoc.RootElement.GetProperty("detail").GetString();
        prodDetail.Should().NotContain("SuperSecretPassword");
        prodDetail.Should().Be(OralExamination.API.Middlewares.GlobalExceptionMiddleware.GenericServerErrorDetail);

        // 2. Lỗi 500 ở Development -> hiển thị chi tiết để debug
        var devContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        devContext.Response.Body = new System.IO.MemoryStream();
        var devMiddleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ => throw new Exception("Detailed stack debug info"),
            logger,
            devEnv);

        await devMiddleware.InvokeAsync(devContext);
        devContext.Response.StatusCode.Should().Be(500);
        devContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var devBody = await new System.IO.StreamReader(devContext.Response.Body).ReadToEndAsync();
        using var devDoc = JsonDocument.Parse(devBody);
        devDoc.RootElement.GetProperty("detail").GetString().Should().Contain("Detailed stack debug info");

        // 3. Lỗi nghiệp vụ OneWayLockException ở Production -> vẫn giữ thông báo tiếng Việt
        var lockContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        lockContext.Response.Body = new System.IO.MemoryStream();
        var lockMiddleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ => throw new OralExamination.Domain.Exceptions.OneWayLockException("Điểm thi đã bị khóa một chiều."),
            logger,
            prodEnv);

        await lockMiddleware.InvokeAsync(lockContext);
        lockContext.Response.StatusCode.Should().Be(403);
        lockContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var lockBody = await new System.IO.StreamReader(lockContext.Response.Body).ReadToEndAsync();
        using var lockDoc = JsonDocument.Parse(lockBody);
        lockDoc.RootElement.GetProperty("detail").GetString().Should().Contain("Điểm thi đã bị khóa một chiều.");

        // 4. InvalidOperationException nội bộ (không phải One-Way Lock) ở Production -> phải trả 500 và che giấu message nhạy cảm
        var ioeContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ioeContext.Response.Body = new System.IO.MemoryStream();
        var ioeMiddleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("Sequence contains no elements in table secret_tokens"),
            logger,
            prodEnv);

        await ioeMiddleware.InvokeAsync(ioeContext);
        ioeContext.Response.StatusCode.Should().Be(500);
        ioeContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var ioeBody = await new System.IO.StreamReader(ioeContext.Response.Body).ReadToEndAsync();
        using var ioeDoc = JsonDocument.Parse(ioeBody);
        var ioeDetail = ioeDoc.RootElement.GetProperty("detail").GetString();
        ioeDetail.Should().NotContain("secret_tokens");
        ioeDetail.Should().Be(OralExamination.API.Middlewares.GlobalExceptionMiddleware.GenericServerErrorDetail);

        // 5. InvalidOperationException có chứa "One-Way Lock" -> trả 403 Forbidden
        var ioeLockContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ioeLockContext.Response.Body = new System.IO.MemoryStream();
        var ioeLockMiddleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("Vi phạm One-Way Lock: không thể cập nhật điểm."),
            logger,
            prodEnv);

        await ioeLockMiddleware.InvokeAsync(ioeLockContext);
        ioeLockContext.Response.StatusCode.Should().Be(403);

        // 6. ArgumentNullException / ArgumentException ở Production -> trả 400 BadRequest chuẩn RFC 7807
        var argContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        argContext.Response.Body = new System.IO.MemoryStream();
        var argMiddleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ => throw new ArgumentException("Mã số sinh viên không hợp lệ."),
            logger,
            prodEnv);

        await argMiddleware.InvokeAsync(argContext);
        argContext.Response.StatusCode.Should().Be(400);
        argContext.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var argBody = await new System.IO.StreamReader(argContext.Response.Body).ReadToEndAsync();
        using var argDoc = JsonDocument.Parse(argBody);
        var argDetail = argDoc.RootElement.GetProperty("detail").GetString();
        argDetail.Should().Contain("Mã số sinh viên không hợp lệ.");
    }

    [Fact(DisplayName = "10. LockExamTicketCommandHandler khóa điểm 1 chiều từng vé thi và OverrideExamScoreCommandHandler hỗ trợ alias Comment")]
    public async Task LockExamTicket_And_OverrideExamScore_Comment_Alias_Should_Work_Correctly()
    {
        await using var db = CreateDbContext();
        var lecturer = new User
        {
            Id = Guid.NewGuid(),
            Email = "lecturer@fpt.edu.vn",
            FullName = "Giảng Viên Chấm Thi",
            Role = UserRole.Lecturer,
            IsActive = true
        };
        var ungradedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SeatNumber = 5,
            IpAddress = "10.0.0.15",
            Status = ExamTicketStatus.Submitted,
            IsLocked = false
        };
        var gradedTicket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            SeatNumber = 6,
            IpAddress = "10.0.0.16",
            Status = ExamTicketStatus.AiGraded,
            IsLocked = false
        };
        var submission = new ExamQuestionSubmission
        {
            Id = Guid.NewGuid(),
            TicketId = gradedTicket.Id,
            ExamQuestionId = Guid.NewGuid(),
            AiScore = 7.00m,
            GradingStatus = "ai_graded",
            SubmittedAt = DateTime.UtcNow
        };
        db.Users.Add(lecturer);
        db.StudentExamTickets.AddRange(ungradedTicket, gradedTicket);
        db.ExamQuestionSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var lockHandler = new OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket.LockExamTicketCommandHandler(db);

        // Không cho phép khóa vé chưa có điểm
        var ungradedRes = await lockHandler.Handle(
            new OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket.LockExamTicketCommand(lecturer.Id, ungradedTicket.Id),
            CancellationToken.None);
        ungradedRes.IsSuccess.Should().BeFalse();
        ungradedRes.Error.Should().StartWith("UNPROCESSABLE:");

        // Override điểm dùng alias Comment (theo §5.8.2 API Contract)
        var criterionId = Guid.NewGuid();
        var overrideHandler = new OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore.OverrideExamScoreCommandHandler(db);
        var overrideRes = await overrideHandler.Handle(
            new OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore.OverrideExamScoreCommand(
                lecturer.Id,
                gradedTicket.Id,
                8.25m,
                "Đã nghe lại Waveform đoạn 01:15, sinh viên giải thích đúng IoC Container.",
                new List<OralExamination.Application.Features.OfficialExams.Commands.OverrideExamScore.OverrideCriterionScoreDto>
                {
                    new(criterionId, 8.25m, LecturerComment: null, AiScore: 7.00m, Comment: "Ghi chú từ trường comment alias")
                }),
            CancellationToken.None);
        overrideRes.IsSuccess.Should().BeTrue();

        var savedDetail = db.LecturerAuditDetails.Single(d => d.CriterionId == criterionId);
        savedDetail.LecturerComment.Should().Be("Ghi chú từ trường comment alias");

        // Khóa điểm vé thi đơn lẻ
        var lockRes = await lockHandler.Handle(
            new OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket.LockExamTicketCommand(lecturer.Id, gradedTicket.Id),
            CancellationToken.None);
        lockRes.IsSuccess.Should().BeTrue();
        lockRes.Value.IsLocked.Should().BeTrue();
        lockRes.Value.Status.Should().Be(ExamTicketStatus.Locked);
    }

    [Fact(DisplayName = "11. OfficialExamDlqReplayWorker tự động replay cả tác vụ PracticeGrading quá hạn 5 phút về IGradingQueueChannel")]
    public async Task OfficialExamDlqReplayWorker_Should_Auto_Replay_Due_PracticeGrading_Tasks()
    {
        await using var db = CreateDbContext();
        var channelLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OralExamination.Infrastructure.Channels.BoundedGradingQueueChannel>.Instance;
        var channel = new OralExamination.Infrastructure.Channels.BoundedGradingQueueChannel(channelLogger);

        var answerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        db.PracticeAnswers.Add(new PracticeAnswer
        {
            Id = answerId,
            SessionId = sessionId,
            QuestionId = questionId,
            AnswerText = "Clean Architecture tách biệt Domain và Infrastructure.",
            Status = "failed",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        var payload = JsonSerializer.Serialize(new OralExamination.Application.Common.Models.GradingTask
        {
            AnswerId = answerId,
            SessionId = sessionId,
            QuestionId = questionId,
            AnswerText = "Clean Architecture tách biệt Domain và Infrastructure.",
            IsFollowUp = false,
            ParentAnswerId = null,
            IsFullSession = false
        });

        db.DeadLetterQueues.Add(new OralExamination.Domain.Entities.DeadLetterQueue
        {
            Id = Guid.NewGuid(),
            TaskType = "PracticeGrading",
            CorrelationId = answerId,
            PayloadJson = payload,
            ErrorMessage = "Gemini 429 rate limit",
            RetryCount = 3,
            Status = "pending",
            CreatedAt = DateTime.UtcNow.AddMinutes(-6),
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var serviceProviderMock = new Moq.Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(OralExamDbContext)))
            .Returns(db);
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(OralExamination.Application.Common.Interfaces.IGradingQueueChannel)))
            .Returns(channel);

        var scopeMock = new Moq.Mock<Microsoft.Extensions.DependencyInjection.IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Moq.Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        scopeFactoryMock.Setup(sf => sf.CreateScope()).Returns(scopeMock.Object);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OralExamination.API.Workers.OfficialExamDlqReplayWorker>.Instance;
        var worker = new OralExamination.API.Workers.OfficialExamDlqReplayWorker(scopeFactoryMock.Object, logger, channel);

        await worker.ReplayDueAsync(CancellationToken.None);

        var dlqEntry = db.DeadLetterQueues.Single();
        dlqEntry.Status.Should().Be("resolved");

        var answer = db.PracticeAnswers.Single();
        answer.Status.Should().Be("pending");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var enumerator = channel.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        enumerator.Current.AnswerId.Should().Be(answerId);
    }

    [Fact(DisplayName = "12. GetNotificationsQueryHandler trả đầy đủ Page, PageSize và Metadata theo chuẩn §5.11")]
    public async Task GetNotificationsQueryHandler_Should_Return_Pagination_And_Metadata()
    {
        await using var db = CreateDbContext();
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "sv.notif@fpt.edu.vn",
            FullName = "Sinh Viên Thông Báo",
            Role = UserRole.Student,
            IsActive = true
        };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            ShiftName = "Ca Sáng",
            RoomLab = "LAB-101",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            Status = "published"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 8,
            IpAddress = "10.0.0.18",
            Status = ExamTicketStatus.Published,
            IsLocked = true
        };
        db.Users.Add(student);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        await db.SaveChangesAsync();

        var handler = new OralExamination.Application.Features.Notifications.Queries.GetNotifications.GetNotificationsQueryHandler(db);
        var res = await handler.Handle(
            new OralExamination.Application.Features.Notifications.Queries.GetNotifications.GetNotificationsQuery(
                student.Id,
                UnreadOnly: false,
                Limit: 20,
                Page: 1,
                PageSize: 10),
            CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Page.Should().Be(1);
        res.Value.PageSize.Should().Be(10);
        res.Value.Items.Should().NotBeEmpty();
        res.Value.Items.First().Metadata.Should().NotBeNull();
        res.Value.Items.First().Metadata!.Should().ContainKey("ticketId");
    }

    [Fact(DisplayName = "13. Chống mạo nhận ActorUserId (Create/UpdateExamStructure, LockExamTicket), GlobalExceptionMiddleware trả 400 cho ArgumentException và đồng bộ RBAC AppealsController")]
    public async Task ActorImpersonationGuard_UpdateStructureAuditLog_AudioObjectKey400_And_AppealsRbac_Should_Work()
    {
        await using var db = CreateDbContext();
        var head = new User
        {
            Id = Guid.NewGuid(),
            Email = "head.guard@fpt.edu.vn",
            FullName = "Trưởng Bộ Môn",
            Role = UserRole.DepartmentHead,
            IsActive = true
        };
        var student = new User
        {
            Id = Guid.NewGuid(),
            Email = "student.guard@fpt.edu.vn",
            FullName = "Sinh Viên",
            Role = UserRole.Student,
            IsActive = true
        };
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Back-End .NET",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            IsActive = true
        };
        db.Users.AddRange(head, student);
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        // 1. Chặn mạo nhận khi ActorUserId == Guid.Empty dù trong DB có tài khoản DepartmentHead đang hoạt động
        var createStructureHandler = new CreateExamStructureCommandHandler(db);
        var impersonatedCreate = await createStructureHandler.Handle(
            new CreateExamStructureCommand(Guid.Empty, course.Id, "Đề giả mạo", 5, 30, "{}"),
            CancellationToken.None);
        impersonatedCreate.IsSuccess.Should().BeFalse();
        impersonatedCreate.Error.Should().Be("FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");

        var lockHandler = new OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket.LockExamTicketCommandHandler(db);
        var impersonatedLock = await lockHandler.Handle(
            new OralExamination.Application.Features.OfficialExams.Commands.LockExamTicket.LockExamTicketCommand(Guid.Empty, Guid.NewGuid()),
            CancellationToken.None);
        impersonatedLock.IsSuccess.Should().BeFalse();
        impersonatedLock.Error.Should().Be("FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");

        // 2. UpdateExamStructureCommandHandler kiểm tra quyền ActorUserId và ghi AuditLog
        var validCreate = await createStructureHandler.Handle(
            new CreateExamStructureCommand(head.Id, course.Id, "Cấu trúc gốc", 5, 30, "{}"),
            CancellationToken.None);
        validCreate.IsSuccess.Should().BeTrue();

        var updateStructureHandler = new UpdateExamStructureCommandHandler(db);
        var emptyActorUpdate = await updateStructureHandler.Handle(
            new UpdateExamStructureCommand(Guid.Empty, validCreate.Value.Id, "Sửa không danh tính", 6, 45, "{}"),
            CancellationToken.None);
        emptyActorUpdate.IsSuccess.Should().BeFalse();
        emptyActorUpdate.Error.Should().Be("FORBIDDEN: Không xác định được danh tính người thực hiện thao tác.");

        var studentUpdate = await updateStructureHandler.Handle(
            new UpdateExamStructureCommand(student.Id, validCreate.Value.Id, "Sinh viên sửa trái phép", 6, 45, "{}"),
            CancellationToken.None);
        studentUpdate.IsSuccess.Should().BeFalse();
        studentUpdate.Error.Should().StartWith("FORBIDDEN:");

        var headUpdate = await updateStructureHandler.Handle(
            new UpdateExamStructureCommand(head.Id, validCreate.Value.Id, "Cấu trúc đã duyệt", 6, 45, "{}"),
            CancellationToken.None);
        headUpdate.IsSuccess.Should().BeTrue();
        db.AuditLogs.Any(a => a.Action == "UPDATE_EXAM_STRUCTURE" && a.EntityId == validCreate.Value.Id && a.UserId == head.Id)
            .Should().BeTrue();

        // 3. OfficialExamAudioObjectKey ném ArgumentException -> GlobalExceptionMiddleware trả HTTP 400 trên Production
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OralExamination.API.Middlewares.GlobalExceptionMiddleware>.Instance;
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production };
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ctx.Response.Body = new System.IO.MemoryStream();

        var middleware = new OralExamination.API.Middlewares.GlobalExceptionMiddleware(
            _ =>
            {
                OralExamination.Application.Features.OfficialExams.OfficialExamAudioObjectKey.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    1,
                    "---");
                return Task.CompletedTask;
            },
            logger,
            prodEnv);

        await middleware.InvokeAsync(ctx);
        ctx.Response.StatusCode.Should().Be(400);
        ctx.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        var body = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("Mã số sinh viên không hợp lệ.");

        // 4. Kiểm tra RBAC Attribute trên AppealsController khớp 100% với Handler
        var appealsType = typeof(global::API.Controllers.v1.AppealsController);
        var reviewAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
            appealsType.GetMethod(nameof(global::API.Controllers.v1.AppealsController.ReviewAppeal))!,
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
        reviewAttr.Roles.Should().Be("department_head,admin");

        var assignAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
            appealsType.GetMethod(nameof(global::API.Controllers.v1.AppealsController.AssignLecturer))!,
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
        assignAttr.Roles.Should().Be("department_head");

        var regradeAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
            appealsType.GetMethod(nameof(global::API.Controllers.v1.AppealsController.Regrade))!,
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
        regradeAttr.Roles.Should().Be("lecturer");

        // 5. Kiểm tra StorageController đã loại bỏ UploadQuestionImage trùng lặp, giữ duy nhất ở QuestionsController
        typeof(global::API.Controllers.v1.StorageController)
            .GetMethod("UploadQuestionImage")
            .Should()
            .BeNull();

        typeof(global::API.Controllers.v1.QuestionsController)
            .GetMethod(nameof(global::API.Controllers.v1.QuestionsController.UploadQuestionImage))
            .Should()
            .NotBeNull();

        // 6. Kiểm tra RBAC AuthorizeAttribute trên CoursesController.UpdateConfiguration và QuestionsController.ReviewDecision
        var coursesType = typeof(global::API.Controllers.v1.CoursesController);
        var updateConfigAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
            coursesType.GetMethod(nameof(global::API.Controllers.v1.CoursesController.UpdateConfiguration))!,
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
        updateConfigAttr.Roles.Should().Be("lecturer,department_head,admin");

        var questionsType = typeof(global::API.Controllers.v1.QuestionsController);
        var reviewDecisionAttr = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(
            questionsType.GetMethod(nameof(global::API.Controllers.v1.QuestionsController.ReviewDecision))!,
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
        reviewDecisionAttr.Roles.Should().Be("department_head,admin");
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Production;
        public string ApplicationName { get; set; } = "OralExamination.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
