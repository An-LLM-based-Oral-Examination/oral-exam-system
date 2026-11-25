using System.Security.Claims;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public sealed class ExamPreparationErrorContractTests
{
    [Theory]
    [InlineData("forbidden", 403)]
    [InlineData("missing", 404)]
    [InlineData("status", 409)]
    [InlineData("validation", 422)]
    public async Task ExistingDomainErrorsUseCorrectProblemDetailsStatus(string kind, int status)
    {
        var error = kind switch { "forbidden" => OfficialExamErrors.Forbidden, "missing" => OfficialExamErrors.ExamSetNotFound,
            "status" => OfficialExamErrors.ExamSetStatusInvalid, _ => "VALIDATION: Nội dung không hợp lệ." };
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<IRequest<Result<object>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result<object>.Failure(error));
        var services = new ServiceCollection(); services.AddLogging(); services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var controller = new ExamPreparationsController(sender.Object) { ControllerContext = new() { HttpContext = new DefaultHttpContext {
            RequestServices = provider, User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "department_head")], "test")) } } };
        var result = (ObjectResult)await controller.List(null, default);
        result.StatusCode.Should().Be(status);
        result.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().Be(error);
    }
}
