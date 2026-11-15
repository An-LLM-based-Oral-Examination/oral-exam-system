using System.Security.Claims;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.DeleteExamShift;
using OralExamination.Application.Features.OfficialExams.Commands.UpdateExamShift;
using OralExamination.Application.Features.OfficialExams.DTOs;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class OfficialShiftMutationControllerTests
{
    private static OfficialExamsController Controller(Mock<ISender> sender) => new(sender.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "examination_officer") }, "test")) } }
    };

    [Theory]
    [InlineData(ShiftMutationErrors.HasTickets, 409, "SHIFT_HAS_TICKETS")]
    [InlineData(ShiftMutationErrors.HasSubmissions, 409, "SHIFT_HAS_SUBMISSIONS")]
    [InlineData(ShiftMutationErrors.Published, 409, "SHIFT_EXAM_PUBLISHED")]
    [InlineData(OfficialExamErrors.Forbidden, 403, "SHIFT_FORBIDDEN")]
    [InlineData(OfficialExamErrors.ShiftNotFound, 404, "SHIFT_RESOURCE_NOT_FOUND")]
    [InlineData("Invalid input", 400, "SHIFT_INVALID_INPUT")]
    public async Task Delete_ReturnsSpecificProblemDetails(string error, int status, string code)
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<DeleteExamShiftCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(error));
        var response = (await Controller(sender).DeleteShift(Guid.NewGuid())).Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(status);
        var problem = response.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be(error);
        problem.Extensions["code"].Should().Be(code);
    }

    [Fact]
    public async Task UpdateAndDelete_Success_Return204()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<DeleteExamShiftCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        sender.Setup(s => s.Send(It.IsAny<UpdateExamShiftCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var controller = Controller(sender);
        (await controller.DeleteShift(Guid.NewGuid())).Should().BeOfType<NoContentResult>();
        (await controller.UpdateShift(Guid.NewGuid(), new UpdateExamShiftRequest("Shift", "Lab", DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(1), Guid.NewGuid(), Guid.NewGuid()))).Should().BeOfType<NoContentResult>();
    }
}
