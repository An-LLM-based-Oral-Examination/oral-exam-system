using System;
using System.Collections.Generic;
using System.IO;
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
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicHierarchy.Commands.JoinClass;
using OralExamination.Application.Features.AcademicHierarchy.DTOs;
using OralExamination.Application.Features.DeadLetterQueue.Commands.ReplayDlqTask;
using OralExamination.Application.Features.MockExams.Commands.StartMockExam;
using OralExamination.Application.Features.MockExams.DTOs;
using OralExamination.Application.Features.Practice.Commands.NextQuestion;
using OralExamination.Application.Features.Practice.Commands.StartPracticeSession;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeAnswer;
using OralExamination.Application.Features.Practice.Commands.SubmitPracticeBatch;
using OralExamination.Application.Features.Practice.DTOs;
using OralExamination.Application.Features.QuestionBank.Commands.BatchApproveQuestions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.Commands.UploadQuestionImage;
using OralExamination.Application.Features.QuestionBank.DTOs;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// EMPIRICAL CHALLENGER TEST SUITE:
/// Kiểm chứng đối kháng thực nghiệm các mã phản hồi HTTP và ràng buộc API Contracts:
/// 1. HTTP 410 Gone (Session Inactivity Timeout > 5 min trong MF-01)
/// 2. HTTP 429 Too Many Requests (Exceeding max_mock_exams_per_day trong MF-02)
/// 3. HTTP 422 Unprocessable Entity (Barem Rubric sum != 10.00 điểm trong MF-03)
/// 4. HTTP 400 Bad Request (Thiếu câu hỏi trong kho khi cấu hình progressive MF-01)
/// 5. HTTP 413 Payload Too Large (Tải ảnh câu hỏi > 5MB)
/// 6. HTTP 415 Unsupported Media Type (Tải tệp sai Magic Bytes không phải PNG/JPG/WEBP)
/// 7. HTTP 403 Forbidden (department_head truy cập DLQ, hoặc sai vai trò replay task)
/// 8. HTTP 400 Bad Request / 401 Unauthorized (Join class sai mật khẩu, hoặc lớp khóa is_join_enabled = false, hoặc thiếu auth)
/// </summary>
public sealed class AdversarialApiStatusCodesAndContractsChallengerTests
{
    private readonly Mock<ISender> _senderMock = new();

    private static void SetControllerContext(ControllerBase controller, Guid userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    #region 1. HTTP 410 Gone — Session Inactivity Timeout > 5 Minutes (MF-01)

    [Fact(DisplayName = "1.1. PracticeController.GetNextQuestion trả về HTTP 410 Gone khi phiên quá 5 phút không tương tác")]
    public async Task GetNextQuestion_Should_Return_410_Gone_When_Inactivity_Exceeds_5_Minutes()
    {
        var controller = new PracticeController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        const string timeoutError = "SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút.";
        _senderMock
            .Setup(s => s.Send(It.Is<NextQuestionCommand>(c => c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NextQuestionResponse>.Failure(timeoutError));

        var result = await controller.GetNextQuestion(sessionId);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status410Gone);
        problemDetails.Title.Should().Be("Session Timed Out");
        problemDetails.Detail.Should().Be(timeoutError);
    }

    [Fact(DisplayName = "1.2. PracticeController.SubmitAnswer trả về HTTP 410 Gone khi phiên quá 5 phút không tương tác")]
    public async Task SubmitAnswer_Should_Return_410_Gone_When_Inactivity_Exceeds_5_Minutes()
    {
        var controller = new PracticeController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        var request = new SubmitPracticeAnswerRequest(
            QuestionId: Guid.NewGuid(),
            StudentId: studentId,
            AnswerText: "Câu trả lời sau khi phiên đã hết hạn",
            IsFollowUp: false,
            ParentAnswerId: null
        );

        const string timeoutError = "SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút.";
        _senderMock
            .Setup(s => s.Send(It.Is<SubmitPracticeAnswerCommand>(c => c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure(timeoutError));

        var result = await controller.SubmitAnswer(sessionId, request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status410Gone);
        problemDetails.Title.Should().Be("Session Timed Out");
        problemDetails.Detail.Should().Be(timeoutError);
    }

    [Fact(DisplayName = "1.3. PracticeController.SubmitBatch trả về HTTP 410 Gone khi phiên quá 5 phút không tương tác")]
    public async Task SubmitBatch_Should_Return_410_Gone_When_Inactivity_Exceeds_5_Minutes()
    {
        var controller = new PracticeController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        var request = new SubmitPracticeBatchRequest(
            StudentId: studentId,
            Answers: new List<BatchAnswerDto>
            {
                new(Guid.NewGuid(), "Câu trả lời batch 1", false, null),
                new(Guid.NewGuid(), "Câu trả lời batch 2", false, null)
            }
        );

        const string timeoutError = "SESSION_TIMEOUT_410: Phiên luyện tập đã kết thúc tự động do không có tương tác trong hơn 5 phút. Điểm các câu đã nộp trước đó được bảo toàn.";
        _senderMock
            .Setup(s => s.Send(It.Is<SubmitPracticeBatchCommand>(c => c.SessionId == sessionId && c.StudentId == studentId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure(timeoutError));

        var result = await controller.SubmitBatch(sessionId, request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status410Gone);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status410Gone);
        problemDetails.Title.Should().Be("Session Timed Out");
        problemDetails.Detail.Should().Be(timeoutError);
    }

    #endregion

    #region 2. HTTP 429 Too Many Requests — Daily Quota Exceeded (MF-02)

    [Fact(DisplayName = "2.1. MockExamsController.StartSession trả về HTTP 429 Too Many Requests khi sinh viên vượt quá hạn ngạch thi thử trong ngày")]
    public async Task StartMockExamSession_Should_Return_429_TooManyRequests_When_DailyQuota_Exceeded()
    {
        var controller = new MockExamsController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        var request = new StartMockExamRequest(CourseId: courseId, HasFollowUp: true);

        const string quotaError = "QUOTA_EXCEEDED:Bạn đã sử dụng hết hạn ngạch thi thử trong ngày (3/3 lượt) cho môn học này.";
        _senderMock
            .Setup(s => s.Send(It.Is<StartMockExamCommand>(c => c.CourseId == courseId && c.StudentId == studentId && c.HasFollowUp), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartMockExamResponse>.Failure(quotaError));

        var result = await controller.StartSession(request);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        var responseValue = objectResult.Value;
        responseValue.Should().NotBeNull();
        var statusProp = responseValue!.GetType().GetProperty("status");
        statusProp.Should().NotBeNull();
        statusProp!.GetValue(responseValue).Should().Be(429);

        var detailProp = responseValue.GetType().GetProperty("detail");
        detailProp.Should().NotBeNull();
        detailProp!.GetValue(responseValue)?.ToString().Should().Contain("Bạn đã sử dụng hết hạn ngạch thi thử trong ngày");
    }

    #endregion

    #region 3. HTTP 422 Unprocessable Entity — Rubric Barem Sum != 10.00 (MF-03)

    [Fact(DisplayName = "3.1. RubricsController.CreateRubric trả về HTTP 422 Unprocessable Entity khi tổng điểm Barem != 10.00 điểm")]
    public async Task CreateRubric_Should_Return_422_UnprocessableEntity_When_Criteria_Sum_Not_10()
    {
        var controller = new RubricsController(_senderMock.Object);
        var lecturerId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetControllerContext(controller, lecturerId, "lecturer");

        var request = new CreateRubricRequestDto(
            CourseId: courseId,
            Name: "Barem lỗi tổng 8.00đ",
            Description: "Mô tả barem",
            Criteria: new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí 1", 5.00m, 0.5m, "Analyze", "Mô tả 1"),
                new("Tiêu chí 2", 3.00m, 0.5m, "Apply", "Mô tả 2") // Sum = 8.00 != 10.00
            }
        );

        const string invariantError = "INVARIANT: Tổng điểm tối đa của các tiêu chí con trong Barem Rubric bắt buộc phải bằng chính xác 10.00 điểm (hiện tại: 8.00đ).";
        _senderMock
            .Setup(s => s.Send(It.IsAny<CreateRubricCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateRubricResponseDto>.Failure(invariantError));

        var result = await controller.CreateRubric(request);

        var unprocessableResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocessableResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var responseValue = unprocessableResult.Value;
        responseValue.Should().NotBeNull();
        var statusProp = responseValue!.GetType().GetProperty("status");
        statusProp.Should().NotBeNull();
        statusProp!.GetValue(responseValue).Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact(DisplayName = "3.2. QuestionsController.BatchApprove trả về HTTP 422 Unprocessable Entity khi câu hỏi có Barem lệch 10.00 điểm")]
    public async Task BatchApproveQuestions_Should_Return_422_UnprocessableEntity_When_Rubric_Sum_Not_10()
    {
        var controller = new QuestionsController(_senderMock.Object);
        var deptHeadId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetControllerContext(controller, deptHeadId, "department_head");

        var rubricWithMismatchedSum = new RubricDraftDto(
            "Barem Lệch",
            "Mô tả barem lệch",
            9.50m,
            new List<RubricCriterionDraftDto>
            {
                new("Tiêu chí 1", 5.00m, 0.5m, "Analyze", "Mô tả 1"),
                new("Tiêu chí 2", 4.50m, 0.5m, "Apply", "Mô tả 2") // Sum = 9.50 != 10.00
            }
        );

        var request = new BatchApproveQuestionsRequestDto(
            CourseId: courseId,
            Questions: new List<ApprovedQuestionItemDto>
            {
                new(
                    "Câu hỏi 1",
                    "Nội dung câu hỏi",
                    "medium",
                    "Analyze",
                    "both",
                    "manual",
                    "Câu trả lời mẫu trên 50 ký tự để vượt qua kiểm tra model answer.",
                    new List<string> { "Từ khóa 1" },
                    false,
                    null,
                    rubricWithMismatchedSum
                )
            }
        );

        const string invariantError = "INVARIANT: Barem Rubric lệch 10.00đ (tổng hiện tại: 9.50đ), không thể phê duyệt.";
        _senderMock
            .Setup(s => s.Send(It.IsAny<BatchApproveQuestionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BatchApproveQuestionsResponseDto>.Failure(invariantError));

        var result = await controller.BatchApprove(request);

        var unprocessableResult = result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocessableResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var responseValue = unprocessableResult.Value;
        responseValue.Should().NotBeNull();
        var statusProp = responseValue!.GetType().GetProperty("status");
        statusProp.Should().NotBeNull();
        statusProp!.GetValue(responseValue).Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion

    #region 4. HTTP 400 Bad Request — Progressive MF-01 Question Shortage

    [Fact(DisplayName = "4.1. PracticeController.StartSession trả về HTTP 400 Bad Request kèm ProblemDetails khi kho đề không đủ câu cho chế độ progressive")]
    public async Task StartPracticeSession_Should_Return_400_BadRequest_When_Questions_Insufficient()
    {
        var controller = new PracticeController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        var request = new StartPracticeSessionRequest(
            StudentId: studentId,
            CourseId: courseId,
            Difficulty: "progressive",
            QuestionCount: 6,
            IsFullSession: true,
            Topic: null
        );

        const string shortageError = "Kho đề hiện tại không đủ câu hỏi để tạo phiên luyện tập Dễ đến Khó: cần 2 Dễ (có 5), 2 Trung bình (có 1), 2 Khó (có 0). Vui lòng liên hệ giảng viên bổ sung câu hỏi.";
        _senderMock
            .Setup(s => s.Send(It.IsAny<StartPracticeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartPracticeSessionResponse>.Failure(shortageError));

        var result = await controller.StartSession(request);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Dữ liệu đầu vào không hợp lệ.");
        problemDetails.Detail.Should().Be(shortageError);
    }

    #endregion

    #region 5. HTTP 413 Payload Too Large — Question Image Upload > 5MB

    [Fact(DisplayName = "5.1. QuestionsController.UploadQuestionImage trả về HTTP 413 Payload Too Large khi ảnh tải lên vượt quá 5MB")]
    public async Task UploadQuestionImage_Should_Return_413_PayloadTooLarge_When_File_Exceeds_5MB()
    {
        var controller = new QuestionsController(_senderMock.Object);
        var lecturerId = Guid.NewGuid();
        SetControllerContext(controller, lecturerId, "lecturer");

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(6 * 1024 * 1024); // 6MB > 5MB
        fileMock.Setup(f => f.FileName).Returns("large-architecture.png");
        fileMock.Setup(f => f.ContentType).Returns("image/png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[16]));

        const string payloadTooLargeError = "PAYLOAD_TOO_LARGE: Dung lượng hình ảnh câu hỏi vượt quá giới hạn tối đa 5MB.";
        _senderMock
            .Setup(s => s.Send(It.IsAny<UploadQuestionImageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadQuestionImageResponseDto>.Failure(payloadTooLargeError));

        var result = await controller.UploadQuestionImage(fileMock.Object, "Mô tả ảnh");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact(DisplayName = "5.2. UploadQuestionImageCommandHandler trả về PAYLOAD_TOO_LARGE khi stream hoặc FileSizeBytes > 5MB")]
    public async Task UploadQuestionImageCommandHandler_Direct_Check_Rejects_Over_5MB()
    {
        var handler = new UploadQuestionImageCommandHandler(storageService: null);

        var dummyStream = new MemoryStream(new byte[16]);
        var command = new UploadQuestionImageCommand(
            dummyStream,
            "oversized.png",
            "image/png",
            FileSizeBytes: 5 * 1024 * 1024 + 1, // 5MB + 1 byte
            Description: "Ảnh quá dung lượng");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("PAYLOAD_TOO_LARGE:");
    }

    #endregion

    #region 6. HTTP 415 Unsupported Media Type — Invalid Magic Bytes

    [Fact(DisplayName = "6.1. QuestionsController.UploadQuestionImage trả về HTTP 415 Unsupported Media Type khi Magic Bytes không phải PNG/JPG/WEBP")]
    public async Task UploadQuestionImage_Should_Return_415_UnsupportedMediaType_When_MagicBytes_Invalid()
    {
        var controller = new QuestionsController(_senderMock.Object);
        var lecturerId = Guid.NewGuid();
        SetControllerContext(controller, lecturerId, "lecturer");

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("fake-image.png");
        fileMock.Setup(f => f.ContentType).Returns("image/png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF magic bytes

        const string unsupportedMediaTypeError = "UNSUPPORTED_MEDIA_TYPE: Định dạng tệp không hợp lệ. Hệ thống chỉ chấp nhận hình ảnh PNG, JPG/JPEG hoặc WEBP hợp lệ.";
        _senderMock
            .Setup(s => s.Send(It.IsAny<UploadQuestionImageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadQuestionImageResponseDto>.Failure(unsupportedMediaTypeError));

        var result = await controller.UploadQuestionImage(fileMock.Object, "Mô tả ảnh");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Fact(DisplayName = "6.2. UploadQuestionImageCommandHandler từ chối các byte header không khớp PNG/JPG/WEBP")]
    public async Task UploadQuestionImageCommandHandler_Direct_Check_Rejects_Non_Image_MagicBytes()
    {
        var handler = new UploadQuestionImageCommandHandler(storageService: null);

        // PDF file signature: %PDF (0x25, 0x50, 0x44, 0x46)
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        using var stream = new MemoryStream(pdfBytes);

        var command = new UploadQuestionImageCommand(
            stream,
            "document-disguised-as-image.png",
            "image/png",
            FileSizeBytes: pdfBytes.Length,
            Description: "Tệp PDF giả mạo đuôi PNG");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().StartWith("UNSUPPORTED_MEDIA_TYPE:");
    }

    #endregion

    #region 7. HTTP 403 Forbidden — DLQ Role Boundary Guard (department_head & Wrong Replay Role)

    [Fact(DisplayName = "7.1. DeadLetterQueueController cấm 100% role department_head truy cập DLQ qua AuthorizeAttribute")]
    public void DeadLetterQueueController_Should_Forbid_DepartmentHead_Role()
    {
        var type = typeof(DeadLetterQueueController);
        var authAttr = type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        authAttr.Should().NotBeNull("DeadLetterQueueController bắt buộc phải có [Authorize]");
        authAttr!.Roles.Should().NotBeNull();
        authAttr.Roles.Should().Contain("admin");
        authAttr.Roles.Should().Contain("examination_officer");
        authAttr.Roles.Should().NotContain("department_head", "Trưởng bộ môn bị cấm hoàn toàn khỏi DLQ theo AGENTS.md và R2");
    }

    [Fact(DisplayName = "7.2. DeadLetterQueueController.ReplayDlqTask trả về HTTP 403 Forbidden khi caller sai vai trò cố replay DLQ task")]
    public async Task ReplayDlqTask_Should_Return_403_Forbidden_When_Caller_Role_Is_Invalid()
    {
        var controller = new DeadLetterQueueController(_senderMock.Object);
        var officerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetControllerContext(controller, officerId, "examination_officer");

        const string forbiddenError = "FORBIDDEN: Chỉ có Quản trị viên (Admin) mới có quyền replay tác vụ Luyện tập hoặc Thi thử.";
        _senderMock
            .Setup(s => s.Send(It.Is<ReplayDlqTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit>.Failure(forbiddenError));

        var result = await controller.ReplayDlqTask(taskId);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var problemDetails = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status403Forbidden);
        problemDetails.Title.Should().Be("Forbidden");
        problemDetails.Detail.Should().Contain(forbiddenError);
    }

    #endregion

    #region 8. HTTP 400 Bad Request / 401 Unauthorized — Class Join Validation & Lock Guard

    [Fact(DisplayName = "8.1. ClassesController.JoinClass trả về HTTP 401 Unauthorized khi không xác định được danh tính sinh viên")]
    public async Task JoinClass_Should_Return_401_Unauthorized_When_UserId_Is_Empty()
    {
        var controller = new ClassesController(_senderMock.Object);
        SetControllerContext(controller, Guid.Empty, "student");

        var request = new JoinClassRequest(JoinCode: "SE1804CODE", Password: "Password123!");

        var result = await controller.JoinClass(request, CancellationToken.None);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var problemDetails = unauthorizedResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problemDetails.Title.Should().Be("Unauthorized");
        problemDetails.Detail.Should().Be("Không xác định được danh tính sinh viên.");
    }

    [Fact(DisplayName = "8.2. ClassesController.JoinClass trả về HTTP 400 Bad Request khi sinh viên nhập sai mật khẩu lớp")]
    public async Task JoinClass_Should_Return_400_BadRequest_When_Password_Is_Incorrect()
    {
        var controller = new ClassesController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        var request = new JoinClassRequest(JoinCode: "SE1804CODE", Password: "WrongPassword!");

        const string invalidPasswordError = "INVALID_PASSWORD: Mật khẩu tham gia lớp học không chính xác.";
        _senderMock
            .Setup(s => s.Send(It.Is<JoinClassCommand>(c => c.StudentId == studentId && c.JoinCode == "SE1804CODE"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JoinClassResponseDto>.Failure(invalidPasswordError));

        var result = await controller.JoinClass(request, CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("Mật khẩu tham gia lớp học không chính xác.");
    }

    [Fact(DisplayName = "8.3. ClassesController.JoinClass trả về HTTP 400 Bad Request khi lớp học bị khóa is_join_enabled = false")]
    public async Task JoinClass_Should_Return_400_BadRequest_When_Join_Is_Disabled()
    {
        var controller = new ClassesController(_senderMock.Object);
        var studentId = Guid.NewGuid();
        SetControllerContext(controller, studentId, "student");

        var request = new JoinClassRequest(JoinCode: "SE1804LOCKED", Password: "Password123!");

        const string joinDisabledError = "JOIN_DISABLED: Lớp học hiện đang khóa tính năng tham gia.";
        _senderMock
            .Setup(s => s.Send(It.Is<JoinClassCommand>(c => c.StudentId == studentId && c.JoinCode == "SE1804LOCKED"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JoinClassResponseDto>.Failure(joinDisabledError));

        var result = await controller.JoinClass(request, CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("Lớp học hiện đang khóa tính năng tham gia.");
    }

    #endregion
}
