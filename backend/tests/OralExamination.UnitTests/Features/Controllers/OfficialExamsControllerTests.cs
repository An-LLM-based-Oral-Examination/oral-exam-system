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
using OralExamination.Application.Features.OfficialExams.Commands.PublishGrades;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetAuditEvidence;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class OfficialExamsControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly OfficialExamsController _controller;

    public OfficialExamsControllerTests()
    {
        _controller = new OfficialExamsController(_senderMock.Object);
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

    [Fact(DisplayName = "1. GetAuditEvidence trả về 200 OK kèm danh sách Evidence Panel")]
    public async Task GetAuditEvidence_Should_Return_200_When_Found()
    {
        var shiftId = Guid.NewGuid();
        var evidenceList = new List<AuditEvidenceItemDto>
        {
            new()
            {
                TicketId = Guid.NewGuid(),
                StudentCode = "SE170001",
                FullName = "Lê Vũ Hoàng",
                SeatNumber = 1,
                Status = "AUDITED",
                AudioR2Url = "https://r2.fpt.edu.vn/exams/shift-1/01_SE170001.webm",
                AudioHashSha256 = "hash123",
                TranscriptWhisper = "Nội dung bài nói thí sinh",
                AiScore = 8.5m,
                FinalScore = 8.5m,
                ConfidenceScore = 0.95m,
                IsSuspicious = false,
                CotTrace = "Chuỗi CoT phân tích",
                IsLocked = false
            }
        };

        _senderMock
            .Setup(s => s.Send(It.Is<GetAuditEvidenceQuery>(q => q.ShiftId == shiftId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<AuditEvidenceItemDto>>.Success(evidenceList));

        var actionResult = await _controller.GetAuditEvidence(shiftId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(evidenceList);
    }

    [Fact(DisplayName = "2. GetAuditEvidence trả về 404 NotFound khi ca thi không tồn tại")]
    public async Task GetAuditEvidence_Should_Return_404_When_Shift_Not_Found()
    {
        var shiftId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<GetAuditEvidenceQuery>(q => q.ShiftId == shiftId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<AuditEvidenceItemDto>>.Failure("Ca thi không tồn tại trong hệ thống."));

        var actionResult = await _controller.GetAuditEvidence(shiftId);

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "3. PublishGrades trả về 200 OK khi công bố điểm thành công")]
    public async Task PublishGrades_Should_Return_200_When_Successful()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, role: "lecturer");

        var shiftId = Guid.NewGuid();
        var request = new PublishGradesRequestDto { LecturerId = lecturerId };

        var responseDto = new PublishGradesResponseDto
        {
            ShiftId = shiftId,
            TotalPublished = 30,
            PublishedAt = DateTime.UtcNow,
            Message = "Đã công bố điểm thành công cho toàn bộ 30/30 sinh viên trong ca thi và kích hoạt khóa một chiều (One-Way Lock)."
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<PublishGradesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PublishGradesResponseDto>.Success(responseDto));

        var actionResult = await _controller.PublishGrades(shiftId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(responseDto);
    }

    [Fact(DisplayName = "4. PublishGrades trả về 422 UnprocessableEntity khi còn sinh viên chưa có điểm hoàn chỉnh")]
    public async Task PublishGrades_Should_Return_422_When_Any_Student_Lacks_Score()
    {
        var lecturerId = Guid.NewGuid();
        SetUserContext(lecturerId, role: "lecturer");

        var shiftId = Guid.NewGuid();
        var request = new PublishGradesRequestDto { LecturerId = lecturerId };

        _senderMock
            .Setup(s => s.Send(It.IsAny<PublishGradesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PublishGradesResponseDto>.Failure("Không thể công bố điểm. Còn sinh viên chưa có điểm hoàn chỉnh."));

        var actionResult = await _controller.PublishGrades(shiftId, request);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }
}
