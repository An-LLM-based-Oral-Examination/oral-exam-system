using System.Security.Claims;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateAudioUploadAuthorization;
using OralExamination.Application.Features.OfficialExams.Commands.SealOfficialExamAudio;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam;
using OralExamination.Application.Features.OfficialExams.DTOs;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public sealed class OfficialExamSubmissionsControllerTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly OfficialExamSubmissionsController _controller;
    private readonly Guid _studentId = Guid.NewGuid();

    public OfficialExamSubmissionsControllerTests()
    {
        _controller = new OfficialExamSubmissionsController(_sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("sub", _studentId.ToString()),
                        new Claim(ClaimTypes.Role, "student")
                    ], "Test"))
                }
            }
        };
        _controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.10.21");
    }

    [Fact]
    public async Task UploadAuthorization_Success_Returns201()
    {
        var ticketId = Guid.NewGuid();
        var response = new OfficialExamAudioUploadAuthorizationResponse(
            "official-exams/key.webm",
            "01_SE170123.webm",
            "https://signed.example",
            DateTime.UtcNow.AddMinutes(10),
            new Dictionary<string, string>());
        _sender.Setup(x => x.Send(It.IsAny<CreateAudioUploadAuthorizationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OfficialExamAudioUploadAuthorizationResponse>.Success(response));

        var result = await _controller.CreateAudioUploadAuthorization(ticketId, CancellationToken.None);

        result.Should().BeOfType<CreatedResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task SealAudio_HashMismatch_Returns409ProblemDetails()
    {
        var ticketId = Guid.NewGuid();
        _sender.Setup(x => x.Send(It.IsAny<SealOfficialExamAudioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OfficialExamAudioSealResponse>.Failure(OfficialExamErrors.AudioHashMismatch));

        var result = await _controller.SealAudio(
            ticketId,
            new SealOfficialExamAudioRequest("key", new string('a', 64)),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflict.Value.Should().BeOfType<ProblemDetails>();
    }

    [Fact]
    public async Task Submit_Success_Returns202WithStatusLocation()
    {
        var ticketId = Guid.NewGuid();
        var response = new SubmitOfficialExamResponse(ticketId, "SUBMITTED", DateTime.UtcNow, 3);
        _sender.Setup(x => x.Send(It.IsAny<SubmitOfficialExamCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitOfficialExamResponse>.Success(response));

        var result = await _controller.Submit(
            ticketId,
            new SubmitOfficialExamRequest([]),
            CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedAtActionResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        accepted.ActionName.Should().Be(nameof(OfficialExamSubmissionsController.GetSubmissionStatus));
    }
}
