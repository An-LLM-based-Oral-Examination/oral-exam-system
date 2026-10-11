using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseConfiguration;
using OralExamination.Application.Features.Courses.DTOs;
using OralExamination.Application.Features.Courses.Queries.GetCourseConfiguration;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng Milestone 1 (Empirical Challenger M1).
/// Thực thi độc lập bởi Challenger 1 (challenger_m1_1).
/// 
/// Trọng tâm thử thách đối kháng:
/// 1. Reflection trên CoursesController:
///    - Phải chứa cả 2 route attribute: "api/v1/courses" và "api/v1/academic/courses".
///    - Phải có [ApiController].
///    - Actions UpdateConfiguration và GetConfiguration phải có đúng route attributes.
/// 
/// 2. Khởi tạo UpdateCourseConfigurationCommand:
///    - 4 tham số (legacy / tương thích ngược): ExamInputMode tự động mang giá trị null.
///    - 5 tham số (mới): nhận diện đầy đủ cả 5 giá trị thuộc tính.
///    - Record immutability và copy 'with' expression.
///    - Reflection kiểm tra tham số thứ 5 có default value = null.
/// 
/// 3. Validation UpdateCourseConfigurationCommandValidator:
///    - Hợp lệ: null, "VoiceOnly", "VoiceWithTranscriptEdit".
///    - Bất hợp lệ: "InvalidMode", "", "   ", "voiceonly" (sai case), trailing space, SQL injection payload.
///    - Kiểm tra thông điệp lỗi chính xác từ validator.
///    - Kiểm tra dải giá trị biên cho CourseId, TranscriptBufferSeconds, MaxFollowUpQuestions.
/// 
/// 4. Handler & Persistence Integration (In-Memory DB):
///    - Update với ExamInputMode mới -> cập nhật CSDL và trả về DTO.
///    - Update với ExamInputMode = null -> giữ nguyên giá trị cũ trong CSDL.
/// </summary>
public class AdversarialMilestone1ChallengerTests
{
    private readonly UpdateCourseConfigurationCommandValidator _validator = new();

    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    #region 1. REFLECTION TRÊN COURSESCONTROLLER

    [Fact(DisplayName = "M1-CHALLENGE-01: Reflection xác nhận CoursesController có đủ cả 2 Route: api/v1/courses và api/v1/academic/courses")]
    public void Reflection_CoursesController_Must_Contain_Both_Routes()
    {
        // Act
        var controllerType = typeof(CoursesController);
        var routeAttributes = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .Select(r => r.Template)
            .ToList();

        // Assert
        routeAttributes.Should().NotBeNull();
        routeAttributes.Should().HaveCountGreaterOrEqualTo(2, "Controller phải có ít nhất 2 thuộc tính [Route]");
        routeAttributes.Should().Contain("api/v1/courses", "Phải hỗ trợ route truyền thống api/v1/courses");
        routeAttributes.Should().Contain("api/v1/academic/courses", "Phải hỗ trợ route chuẩn kiến trúc api/v1/academic/courses");
    }

    [Fact(DisplayName = "M1-CHALLENGE-02: Reflection xác nhận CoursesController có [ApiController] và các Route Action tương ứng")]
    public void Reflection_CoursesController_Must_Have_ApiController_And_Action_Attributes()
    {
        // Act
        var controllerType = typeof(CoursesController);
        var hasApiController = controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), false).Any();

        var updateMethod = controllerType.GetMethod(nameof(CoursesController.UpdateConfiguration));
        var getMethod = controllerType.GetMethod(nameof(CoursesController.GetConfiguration));

        // Assert
        hasApiController.Should().BeTrue("CoursesController phải có [ApiController]");

        updateMethod.Should().NotBeNull();
        var httpPutAttr = updateMethod!.GetCustomAttributes(typeof(HttpPutAttribute), false).Cast<HttpPutAttribute>().FirstOrDefault();
        httpPutAttr.Should().NotBeNull();
        httpPutAttr!.Template.Should().Be("{id:guid}/configurations");

        getMethod.Should().NotBeNull();
        var httpGetAttr = getMethod!.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().FirstOrDefault();
        httpGetAttr.Should().NotBeNull();
        httpGetAttr!.Template.Should().Be("{id:guid}/configurations");
    }

    #endregion

    #region 2. KHỞI TẠO UPDATECOURSECONFIGURATIONCOMMAND (LEGACY 4-PARAMS VÀ NEW 5-PARAMS)

    [Fact(DisplayName = "M1-CHALLENGE-03: Khởi tạo Command với 4 tham số (legacy) bảo toàn ExamInputMode = null")]
    public void Command_Instantiation_With_4_Params_Should_Set_ExamInputMode_To_Null()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        const int bufferSeconds = 60;
        const int maxFollowUp = 2;
        const bool hasFollowUp = true;

        // Act
        var command = new UpdateCourseConfigurationCommand(
            courseId,
            bufferSeconds,
            maxFollowUp,
            hasFollowUp
        );

        // Assert
        command.CourseId.Should().Be(courseId);
        command.TranscriptBufferSeconds.Should().Be(bufferSeconds);
        command.MaxFollowUpQuestions.Should().Be(maxFollowUp);
        command.HasFollowUp.Should().Be(hasFollowUp);
        command.ExamInputMode.Should().BeNull("Constructor 4 tham số phải gán mặc định ExamInputMode = null để tương thích ngược");
    }

    [Theory(DisplayName = "M1-CHALLENGE-04: Khởi tạo Command với 5 tham số nhận diện chính xác các chế độ thi")]
    [InlineData("VoiceOnly")]
    [InlineData("VoiceWithTranscriptEdit")]
    public void Command_Instantiation_With_5_Params_Should_Set_All_Properties_Correctly(string examInputMode)
    {
        // Arrange
        var courseId = Guid.NewGuid();
        const int bufferSeconds = 90;
        const int maxFollowUp = 1;
        const bool hasFollowUp = false;

        // Act
        var command = new UpdateCourseConfigurationCommand(
            courseId,
            bufferSeconds,
            maxFollowUp,
            hasFollowUp,
            examInputMode
        );

        // Assert
        command.CourseId.Should().Be(courseId);
        command.TranscriptBufferSeconds.Should().Be(bufferSeconds);
        command.MaxFollowUpQuestions.Should().Be(maxFollowUp);
        command.HasFollowUp.Should().Be(hasFollowUp);
        command.ExamInputMode.Should().Be(examInputMode);
    }

    [Fact(DisplayName = "M1-CHALLENGE-05: Record immutability và copy 'with' expression hoạt động chính xác")]
    public void Command_With_Expression_Should_Support_NonDestructive_Mutation()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var originalCommand = new UpdateCourseConfigurationCommand(courseId, 60, 1, false);

        // Act
        var modifiedCommand = originalCommand with { ExamInputMode = ExamInputMode.VoiceOnly };

        // Assert
        originalCommand.ExamInputMode.Should().BeNull("Record gốc không được thay đổi (immutability)");
        modifiedCommand.ExamInputMode.Should().Be("VoiceOnly");
        modifiedCommand.CourseId.Should().Be(courseId);
        modifiedCommand.TranscriptBufferSeconds.Should().Be(60);
    }

    [Fact(DisplayName = "M1-CHALLENGE-06: Reflection xác nhận constructor của Command có tham số thứ 5 tùy chọn với default value = null")]
    public void Command_Reflection_Constructor_Should_Have_Optional_Fifth_Parameter()
    {
        // Arrange
        var commandType = typeof(UpdateCourseConfigurationCommand);
        var constructors = commandType.GetConstructors();

        constructors.Should().ContainSingle("Record chỉ nên có một primary constructor");
        var ctor = constructors[0];
        var parameters = ctor.GetParameters();

        // Assert
        parameters.Should().HaveCount(5, "Constructor phải có 5 tham số");
        var fifthParam = parameters[4];
        fifthParam.Name.Should().Be("ExamInputMode");
        fifthParam.ParameterType.Should().Be(typeof(string));
        fifthParam.IsOptional.Should().BeTrue("Tham số thứ 5 phải là optional để không phá vỡ callers 4 tham số");
        fifthParam.DefaultValue.Should().BeNull("Default value của tham số thứ 5 phải là null");
    }

    #endregion

    #region 3. VALIDATION CỦA UPDATECOURSECONFIGURATIONCOMMANDVALIDATOR

    [Theory(DisplayName = "M1-CHALLENGE-07: Validator PASS với các giá trị hợp lệ của ExamInputMode: null, VoiceOnly, VoiceWithTranscriptEdit")]
    [InlineData(null)]
    [InlineData(ExamInputMode.VoiceOnly)]
    [InlineData(ExamInputMode.VoiceWithTranscriptEdit)]
    public void Validator_Should_Pass_For_Valid_ExamInputMode(string? validMode)
    {
        // Arrange
        var command = new UpdateCourseConfigurationCommand(
            Guid.NewGuid(),
            60,
            1,
            true,
            validMode
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue($"ExamInputMode '{validMode ?? "null"}' phải hợp lệ");
        result.Errors.Should().BeEmpty();
    }

    [Theory(DisplayName = "M1-CHALLENGE-08: Validator REJECT với các giá trị không hợp lệ của ExamInputMode: InvalidMode, chuỗi rỗng, khoảng trắng, sai case, injection")]
    [InlineData("InvalidMode")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("voiceonly")]
    [InlineData("VOICE_ONLY")]
    [InlineData("VoiceAndTextInput")]
    [InlineData("VoiceAndTextInput ")]
    [InlineData(" VoiceOnly")]
    [InlineData("RandomString123")]
    [InlineData("' OR '1'='1")]
    public void Validator_Should_Reject_For_Invalid_ExamInputMode(string invalidMode)
    {
        // Arrange
        var command = new UpdateCourseConfigurationCommand(
            Guid.NewGuid(),
            60,
            1,
            true,
            invalidMode
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse($"ExamInputMode '{invalidMode}' phải bị từ chối");
        var error = result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateCourseConfigurationCommand.ExamInputMode)).Subject;
        error.ErrorMessage.Should().Be("Phương thức thi ExamInputMode không hợp lệ. Chỉ chấp nhận: VoiceOnly, VoiceWithTranscriptEdit.");
    }

    [Theory(DisplayName = "M1-CHALLENGE-09: Validator kiểm tra nghiêm ngặt các dải biên khác (BufferSeconds, MaxFollowUp, CourseId)")]
    [InlineData(0, 60, 1, false, "Mã môn học không được để trống.")] // Guid.Empty
    [InlineData(1, 9, 1, false, "Thời gian đệm chỉnh sửa transcript phải nằm trong khoảng từ 10 đến 300 giây.")] // Buffer < 10
    [InlineData(1, 301, 1, false, "Thời gian đệm chỉnh sửa transcript phải nằm trong khoảng từ 10 đến 300 giây.")] // Buffer > 300
    [InlineData(1, 60, 0, false, "Số lượng câu hỏi phụ tối đa phải nằm trong khoảng từ 1 đến 5 câu.")] // MaxFollowUp < 1
    [InlineData(1, 60, 6, false, "Số lượng câu hỏi phụ tối đa phải nằm trong khoảng từ 1 đến 5 câu.")] // MaxFollowUp > 5
    public void Validator_Should_Enforce_Other_Domain_Invariants(int idType, int buffer, int maxFollowUp, bool expectedValid, string expectedMessage)
    {
        // Arrange
        var courseId = idType == 0 ? Guid.Empty : Guid.NewGuid();
        var command = new UpdateCourseConfigurationCommand(
            courseId,
            buffer,
            maxFollowUp,
            true,
            ExamInputMode.VoiceWithTranscriptEdit
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().Be(expectedValid);
        if (!expectedValid)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage == expectedMessage);
        }
    }

    #endregion

    #region 4. HANDLER & PERSISTENCE INTEGRATION TESTS (IN-MEMORY DB)

    [Fact(DisplayName = "M1-CHALLENGE-10: Handler cập nhật thành công ExamInputMode mới vào DB và phản ánh qua DTO")]
    public async Task Handler_Should_Persist_New_ExamInputMode_To_Database()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        dbContext.Semesters.Add(semester);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "SWD392",
            Name = "Software Architecture & Design",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = false,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit,
            IsActive = true
        };
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(dbContext);
        var command = new UpdateCourseConfigurationCommand(
            course.Id,
            90,
            2,
            true,
            ExamInputMode.VoiceOnly
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ExamInputMode.Should().Be(ExamInputMode.VoiceOnly);
        result.Value.TranscriptBufferSeconds.Should().Be(90);
        result.Value.MaxFollowUpQuestions.Should().Be(2);

        // Kiểm tra đối tượng thực tế trong CSDL
        var updatedCourseInDb = await dbContext.Courses.FindAsync(course.Id);
        updatedCourseInDb.Should().NotBeNull();
        updatedCourseInDb!.ExamInputMode.Should().Be(ExamInputMode.VoiceOnly);
        updatedCourseInDb.TranscriptBufferSeconds.Should().Be(90);
        updatedCourseInDb.MaxFollowUpQuestions.Should().Be(2);
    }

    [Fact(DisplayName = "M1-CHALLENGE-11: Handler khi nhận ExamInputMode = null phải giữ nguyên giá trị cũ trong DB")]
    public async Task Handler_Should_Retain_Existing_ExamInputMode_When_Command_Has_Null()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        dbContext.Semesters.Add(semester);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Back-End Applications with .NET",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = false,
            TranscriptBufferSeconds = 60,
            MaxFollowUpQuestions = 1,
            ExamInputMode = ExamInputMode.VoiceWithTranscriptEdit, // Giá trị ban đầu
            IsActive = true
        };
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateCourseConfigurationCommandHandler(dbContext);
        // Command legacy hoặc không truyền ExamInputMode (null)
        var command = new UpdateCourseConfigurationCommand(
            course.Id,
            45,
            1,
            false,
            null // Không thay đổi chế độ thi
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ExamInputMode.Should().Be(ExamInputMode.VoiceWithTranscriptEdit, "Giá trị ExamInputMode cũ phải được bảo toàn");

        var courseInDb = await dbContext.Courses.FindAsync(course.Id);
        courseInDb!.ExamInputMode.Should().Be(ExamInputMode.VoiceWithTranscriptEdit);
        courseInDb.TranscriptBufferSeconds.Should().Be(45);
    }

    [Fact(DisplayName = "M1-CHALLENGE-12: GetCourseConfigurationQuery phản ánh chính xác ExamInputMode từ DB")]
    public async Task Query_Should_Return_ExamInputMode_From_Database()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Code = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        dbContext.Semesters.Add(semester);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "EXE201",
            Name = "Experiential Entrepreneurship",
            Credits = 3,
            SemesterId = semester.Id,
            HasFollowUp = true,
            TranscriptBufferSeconds = 120,
            MaxFollowUpQuestions = 2,
            ExamInputMode = ExamInputMode.VoiceOnly,
            IsActive = true
        };
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync();

        var queryHandler = new GetCourseConfigurationQueryHandler(dbContext);
        var query = new GetCourseConfigurationQuery(course.Id);

        // Act
        var result = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ExamInputMode.Should().Be(ExamInputMode.VoiceOnly);
        result.Value.CourseCode.Should().Be("EXE201");
    }

    #endregion
}
