using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class CoursesControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly CoursesController _controller;

    public CoursesControllerTests()
    {
        _controller = new CoursesController(_senderMock.Object);
    }

    [Fact(DisplayName = "1. UpdateConfiguration trả về 200 OK khi cập nhật cấu hình môn học thành công")]
    public async Task UpdateConfiguration_Should_Return_200_When_Successful()
    {
        var courseId = Guid.NewGuid();
        var request = new UpdateCourseConfigRequestDto
        {
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = true
        };

        var responseDto = new CourseConfigurationDto
        {
            CourseId = courseId,
            CourseCode = "PRN231",
            CourseName = "Building Cross-Platform Back-End Applications with .NET",
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 2,
            HasFollowUp = true,
            IsActive = true
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<UpdateCourseConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseConfigurationDto>.Success(responseDto));

        var actionResult = await _controller.UpdateConfiguration(courseId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(responseDto);
    }

    [Fact(DisplayName = "2. UpdateConfiguration trả về 422 UnprocessableEntity khi Command thất bại")]
    public async Task UpdateConfiguration_Should_Return_422_When_Failed()
    {
        var courseId = Guid.NewGuid();
        var request = new UpdateCourseConfigRequestDto
        {
            TranscriptBufferSeconds = 5, // Invalid (< 10)
            MaxFollowUpQuestions = 10   // Invalid (> 5)
        };

        _senderMock
            .Setup(s => s.Send(It.IsAny<UpdateCourseConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseConfigurationDto>.Failure("Thời gian đệm phải từ 10 đến 300 giây."));

        var actionResult = await _controller.UpdateConfiguration(courseId, request);

        var unprocResult = actionResult.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        unprocResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact(DisplayName = "3. GetConfiguration trả về 200 OK khi môn học tồn tại")]
    public async Task GetConfiguration_Should_Return_200_When_Found()
    {
        var courseId = Guid.NewGuid();
        var dto = new CourseConfigurationDto
        {
            CourseId = courseId,
            CourseCode = "SWD392",
            CourseName = "Software Architecture & Design",
            TranscriptBufferSeconds = 45,
            MaxFollowUpQuestions = 1,
            HasFollowUp = false,
            IsActive = true
        };

        _senderMock
            .Setup(s => s.Send(It.Is<GetCourseConfigurationQuery>(q => q.CourseId == courseId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseConfigurationDto>.Success(dto));

        var actionResult = await _controller.GetConfiguration(courseId);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(dto);
    }

    [Fact(DisplayName = "4. GetConfiguration trả về 404 NotFound khi môn học không tồn tại")]
    public async Task GetConfiguration_Should_Return_404_When_Not_Found()
    {
        var courseId = Guid.NewGuid();

        _senderMock
            .Setup(s => s.Send(It.Is<GetCourseConfigurationQuery>(q => q.CourseId == courseId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CourseConfigurationDto>.Failure("Môn học không tồn tại trong hệ thống."));

        var actionResult = await _controller.GetConfiguration(courseId);

        var notFoundResult = actionResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "5. CoursesController phải hỗ trợ định tuyến kép: /api/v1/courses và /api/v1/academic/courses")]
    public void CoursesController_Should_Support_Dual_Routing()
    {
        var routeAttributes = typeof(CoursesController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .Select(r => r.Template)
            .ToList();

        routeAttributes.Should().Contain("api/v1/courses");
        routeAttributes.Should().Contain("api/v1/academic/courses");
    }

    [Fact(DisplayName = "6. UpdateConfiguration truyền chính xác ExamInputMode vào Command")]
    public async Task UpdateConfiguration_Should_Pass_ExamInputMode_To_Command()
    {
        var courseId = Guid.NewGuid();
        var request = new UpdateCourseConfigRequestDto
        {
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            HasFollowUp = false,
            ExamInputMode = "VoiceOnly"
        };

        var responseDto = new CourseConfigurationDto
        {
            CourseId = courseId,
            CourseCode = "SWD392",
            CourseName = "Software Architecture & Design",
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            HasFollowUp = false,
            ExamInputMode = "VoiceOnly",
            IsActive = true
        };

        UpdateCourseConfigurationCommand capturedCommand = null!;
        _senderMock
            .Setup(s => s.Send(It.IsAny<UpdateCourseConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<CourseConfigurationDto>>, CancellationToken>((cmd, _) =>
            {
                capturedCommand = (UpdateCourseConfigurationCommand)cmd;
            })
            .ReturnsAsync(Result<CourseConfigurationDto>.Success(responseDto));

        var actionResult = await _controller.UpdateConfiguration(courseId, request);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(responseDto);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ExamInputMode.Should().Be("VoiceOnly");
        capturedCommand.TranscriptBufferSeconds.Should().Be(60);
        capturedCommand.MaxFollowUpQuestions.Should().Be(1);
    }
}
