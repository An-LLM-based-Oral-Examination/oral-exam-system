using System.Security.Claims;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.AcademicCatalog.Commands.AssignProgramDepartmentHead;
using OralExamination.Application.Features.AcademicCatalog.Commands.SyncSemesterCourseOfferings;
using OralExamination.Application.Features.AcademicCatalog.DTOs;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public sealed class AcademicCatalogControllerTests
{
    [Fact]
    public async Task SyncCourseOfferings_Should_Dispatch_Actor_Semester_And_Selected_Courses()
    {
        var sender = new Mock<ISender>();
        var actorId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var selectedIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        SyncSemesterCourseOfferingsCommand? captured = null;
        sender.Setup(s => s.Send(
                It.IsAny<SyncSemesterCourseOfferingsCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<SyncCourseOfferingsDto>>, CancellationToken>((request, _) =>
                captured = (SyncSemesterCourseOfferingsCommand)request)
            .ReturnsAsync(Result<SyncCourseOfferingsDto>.Success(new SyncCourseOfferingsDto
            {
                SemesterId = semesterId
            }));

        var controller = new SemestersController(sender.Object)
        {
            ControllerContext = ControllerContextFor(actorId)
        };

        var response = await controller.SyncCourseOfferings(
            semesterId,
            new SyncSemesterCourseOfferingsRequest { SelectedCourseIds = selectedIds });

        response.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.ActorUserId.Should().Be(actorId);
        captured.SemesterId.Should().Be(semesterId);
        captured.SelectedCourseIds.Should().BeEquivalentTo(selectedIds);
    }

    [Fact]
    public async Task AssignProgramHead_Should_Return_404_ProblemDetails_When_Program_Does_Not_Exist()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.IsAny<AssignProgramDepartmentHeadCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AcademicProgramDto>.Failure("NOT_FOUND: Không tìm thấy ngành đào tạo."));

        var controller = new AcademicProgramsController(sender.Object)
        {
            ControllerContext = ControllerContextFor(Guid.NewGuid())
        };

        var response = await controller.AssignDepartmentHead(
            Guid.NewGuid(),
            new AssignProgramDepartmentHeadRequest(Guid.NewGuid()));

        var objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
    }

    private static ControllerContext ControllerContextFor(Guid actorId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, actorId.ToString()) },
                "test"))
        }
    };
}
