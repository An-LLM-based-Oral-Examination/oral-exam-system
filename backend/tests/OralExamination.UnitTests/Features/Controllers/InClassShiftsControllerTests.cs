using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.AdjustInClassSubmissionScore;
using OralExamination.Application.Features.InClassShifts.Commands.CreateInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.EnterInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;
using OralExamination.Application.Features.InClassShifts.Commands.SubmitInClassShiftAnswer;
using OralExamination.Application.Features.InClassShifts.DTOs;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftById;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftCurrentPassword;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftSubmissions;
using OralExamination.Application.Features.InClassShifts.Queries.GetInClassShiftsByClass;
using OralExamination.Application.Features.InClassShifts.Queries.GetStudentInClassSubmission;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class InClassShiftsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly InClassShiftsController _controller;

    public InClassShiftsControllerTests()
    {
        _controller = new InClassShiftsController(_senderMock.Object);
    }

    private void SetUserContext(Guid userId, string role = "lecturer")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact(DisplayName = "1. CreateShift returns 201 Created on success")]
    public async Task CreateShift_Should_Return_201_When_Successful()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, "lecturer");

        var classId = Guid.NewGuid();
        var request = new CreateInClassShiftRequest(classId, "Ca thi Sprint 1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30));

        var responseDto = new InClassExamShiftResponse(
            Guid.NewGuid(),
            classId,
            "SE1701",
            "Lớp KTPM 1701",
            lecturerId,
            "Giảng viên A",
            "Ca thi Sprint 1",
            request.StartTime,
            request.EndTime,
            "SCHEDULED",
            false,
            null,
            DateTime.UtcNow,
            0,
            0
        );

        _senderMock.Setup(s => s.Send(It.IsAny<CreateInClassShiftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InClassExamShiftResponse>.Success(responseDto));

        // Act
        var result = await _controller.CreateShift(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(responseDto);
    }

    [Fact(DisplayName = "2. CreateShift returns 403 Forbidden when lecturer not authorized")]
    public async Task CreateShift_Should_Return_403_When_Not_Authorized()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, "lecturer");

        var request = new CreateInClassShiftRequest(Guid.NewGuid(), "Ca thi không có quyền", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30));

        _senderMock.Setup(s => s.Send(It.IsAny<CreateInClassShiftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InClassExamShiftResponse>.Failure(InClassShiftErrors.LecturerNotAuthorized));

        // Act
        var result = await _controller.CreateShift(request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "3. GetShiftById returns 200 OK when found")]
    public async Task GetShiftById_Should_Return_200_When_Found()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, "lecturer");

        var shiftId = Guid.NewGuid();
        var detailDto = new InClassShiftDetailResponse(
            shiftId,
            Guid.NewGuid(),
            "SE1701",
            "Lớp KTPM",
            lecturerId,
            "Giảng viên A",
            "Ca thi mẫu",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30),
            "IN_PROGRESS",
            false,
            null,
            DateTime.UtcNow,
            new List<InClassShiftParticipantResponse>(),
            new List<InClassExamSubmissionResponse>()
        );

        _senderMock.Setup(s => s.Send(It.IsAny<GetInClassShiftByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InClassShiftDetailResponse>.Success(detailDto));

        // Act
        var result = await _controller.GetShiftById(shiftId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(detailDto);
    }

    [Fact(DisplayName = "4. GetCurrentPassword returns 200 OK with OTP and remaining seconds")]
    public async Task GetCurrentPassword_Should_Return_200_When_Valid()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, "lecturer");

        var shiftId = Guid.NewGuid();
        var passwordDto = new ActiveShiftPasswordResponse(shiftId, "123456", 240, DateTime.UtcNow, DateTime.UtcNow.AddSeconds(240));

        _senderMock.Setup(s => s.Send(It.IsAny<GetInClassShiftCurrentPasswordQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ActiveShiftPasswordResponse>.Success(passwordDto));

        // Act
        var result = await _controller.GetCurrentPassword(shiftId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(passwordDto);
    }

    [Fact(DisplayName = "5. EnterShift returns 200 OK on correct OTP")]
    public async Task EnterShift_Should_Return_200_When_Otp_Valid()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId, "student");

        var shiftId = Guid.NewGuid();
        var joinDto = new JoinInClassShiftResponse(shiftId, studentId, "Ca thi SE1701", "JOINED", DateTime.UtcNow);

        _senderMock.Setup(s => s.Send(It.IsAny<EnterInClassShiftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JoinInClassShiftResponse>.Success(joinDto));

        // Act
        var result = await _controller.EnterShift(shiftId, new EnterInClassShiftRequest("123456"), CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(joinDto);
    }

    [Fact(DisplayName = "6. EnterShift returns 401 Unauthorized on wrong OTP")]
    public async Task EnterShift_Should_Return_401_When_Otp_Invalid()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId, "student");

        var shiftId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<EnterInClassShiftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JoinInClassShiftResponse>.Failure(InClassShiftErrors.InvalidOtp));

        // Act
        var result = await _controller.EnterShift(shiftId, new EnterInClassShiftRequest("000000"), CancellationToken.None);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact(DisplayName = "7. SubmitAnswer returns 200 OK on valid submission")]
    public async Task SubmitAnswer_Should_Return_200_When_Valid()
    {
        var studentId = Guid.NewGuid();
        SetUserContext(studentId, "student");

        var shiftId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var subDto = new SubmitInClassAnswerResponse(Guid.NewGuid(), shiftId, studentId, questionId, "SUBMITTED", DateTime.UtcNow);

        _senderMock.Setup(s => s.Send(It.IsAny<SubmitInClassShiftAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitInClassAnswerResponse>.Success(subDto));

        // Act
        var result = await _controller.SubmitAnswer(shiftId, new SubmitInClassAnswerRequest(questionId, "Nội dung bài nói vấn đáp"), CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(subDto);
    }

    [Fact(DisplayName = "8. FinalizeGrades returns 200 OK and locks shift")]
    public async Task FinalizeGrades_Should_Return_200_And_LockShift()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, "lecturer");

        var shiftId = Guid.NewGuid();
        var finalizeDto = new FinalizeInClassGradesResponse(shiftId, "Ca thi Sprint 1", "FINALIZED", true, DateTime.UtcNow);

        _senderMock.Setup(s => s.Send(It.IsAny<FinalizeInClassShiftGradesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FinalizeInClassGradesResponse>.Success(finalizeDto));

        // Act
        var result = await _controller.FinalizeGrades(shiftId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(finalizeDto);
    }

    [Fact(DisplayName = "9. AdjustSubmissionScore returns 403 Forbidden when shift is locked")]
    public async Task AdjustSubmissionScore_Should_Return_403_When_ShiftLocked()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, "lecturer");

        var shiftId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<AdjustInClassSubmissionScoreCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InClassExamSubmissionResponse>.Failure(InClassShiftErrors.ShiftLocked));

        // Act
        var result = await _controller.AdjustSubmissionScore(shiftId, submissionId, new AdjustSubmissionScoreRequest(9.0m, "Ghi chú"), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
