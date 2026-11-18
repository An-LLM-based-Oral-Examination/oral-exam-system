using System.Security.Claims;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Courses.Queries.GetCourseClos;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses;

public sealed class CoursesControllerCloActorTests
{
    [Fact]
    public async Task GetCourseClos_PassesAuthenticatedLecturerIdToOwnershipCheck()
    {
        var lecturerId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        GetCourseClosQuery? captured = null;
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<GetCourseClosQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<List<CourseCloDto>>>, CancellationToken>((query, _) =>
                captured = (GetCourseClosQuery)query)
            .ReturnsAsync(Result<List<CourseCloDto>>.Success([]));

        var controller = new CoursesController(sender.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, lecturerId.ToString()),
                     new Claim(ClaimTypes.Role, "lecturer")],
                    "test", ClaimTypes.Name, ClaimTypes.Role))
                }
            }
        };

        await controller.GetCourseClos(courseId, true);

        captured.Should().NotBeNull();
        captured!.ActorRole.Should().Be($"lecturer:{lecturerId}");
        captured.CourseId.Should().Be(courseId);
    }
}
