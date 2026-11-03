using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Features.Courses.Commands.CreateCourseClo;
using OralExamination.Application.Features.Courses.Commands.DeleteCourseClo;
using OralExamination.Application.Features.Courses.Commands.UpdateCourseClo;
using OralExamination.Application.Features.Courses.Queries.GetCourseClos;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.Courses;

public class CourseClosTests
{
    private OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "CLO-01: Thêm mới CLO thành công với dữ liệu hợp lệ")]
    public async Task CreateCourseClo_With_Valid_Data_Should_Succeed()
    {
        await using var context = CreateDbContext();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRN231",
            Name = "Building Cross-Platform Apps",
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new CreateCourseCloCommandHandler(context);
        var command = new CreateCourseCloCommand(
            course.Id,
            "CLO1",
            "Hiểu kiến trúc RESTful Web API trong .NET 8",
            BloomLevel.Understand,
            1,
            UserRole.DepartmentHead);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CloCode.Should().Be("CLO1");
        result.Value.BloomLevel.Should().Be(BloomLevel.Understand);
        result.Value.CourseId.Should().Be(course.Id);

        var savedInDb = await context.CourseClos.FirstOrDefaultAsync(c => c.Id == result.Value.Id);
        savedInDb.Should().NotBeNull();
        savedInDb!.CloCode.Should().Be("CLO1");
    }

    [Fact(DisplayName = "CLO-02: Chặn tạo trùng lặp mã CLO trong cùng một môn học")]
    public async Task CreateCourseClo_With_Duplicate_Code_Should_Fail()
    {
        await using var context = CreateDbContext();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "PRF192",
            Name = "Programming Fundamentals",
            IsActive = true
        };
        context.Courses.Add(course);

        var existingClo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            CloCode = "CLO1",
            Description = "Kiến thức cơ bản C",
            BloomLevel = BloomLevel.Remember,
            OrderIndex = 1
        };
        context.CourseClos.Add(existingClo);
        await context.SaveChangesAsync();

        var handler = new CreateCourseCloCommandHandler(context);
        var command = new CreateCourseCloCommand(
            course.Id,
            "CLO1",
            "Mô tả mới trùng mã",
            BloomLevel.Understand,
            2,
            UserRole.ExaminationOfficer);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("đã tồn tại trong môn học này");
    }

    [Fact(DisplayName = "CLO-03: Vai trò không được phép sẽ bị từ chối (Forbidden)")]
    public async Task CreateCourseClo_With_Unauthorized_Role_Should_Fail()
    {
        await using var context = CreateDbContext();

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Code = "CSD201",
            Name = "Data Structures",
            IsActive = true
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync();

        var handler = new CreateCourseCloCommandHandler(context);
        var command = new CreateCourseCloCommand(
            course.Id,
            "CLO1",
            "Mô tả thuật toán",
            BloomLevel.Apply,
            1,
            UserRole.Student);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("FORBIDDEN");
    }

    [Fact(DisplayName = "CLO-04: Truy vấn danh sách CLOs sắp xếp đúng thứ tự OrderIndex")]
    public async Task GetCourseClos_Should_Return_Ordered_List()
    {
        await using var context = CreateDbContext();

        var courseId = Guid.NewGuid();
        context.CourseClos.AddRange(
            new CourseClo { Id = Guid.NewGuid(), CourseId = courseId, CloCode = "CLO3", Description = "Desc 3", BloomLevel = BloomLevel.Analyze, OrderIndex = 3, IsActive = true },
            new CourseClo { Id = Guid.NewGuid(), CourseId = courseId, CloCode = "CLO1", Description = "Desc 1", BloomLevel = BloomLevel.Remember, OrderIndex = 1, IsActive = true },
            new CourseClo { Id = Guid.NewGuid(), CourseId = courseId, CloCode = "CLO2", Description = "Desc 2", BloomLevel = BloomLevel.Apply, OrderIndex = 2, IsActive = false }
        );
        await context.SaveChangesAsync();

        var handler = new GetCourseClosQueryHandler(context);

        // All CLOs
        var allResult = await handler.Handle(new GetCourseClosQuery(courseId, false), CancellationToken.None);
        allResult.IsSuccess.Should().BeTrue();
        allResult.Value.Should().HaveCount(3);
        allResult.Value.Select(c => c.CloCode).Should().ContainInOrder("CLO1", "CLO2", "CLO3");

        // Active only
        var activeResult = await handler.Handle(new GetCourseClosQuery(courseId, true), CancellationToken.None);
        activeResult.IsSuccess.Should().BeTrue();
        activeResult.Value.Should().HaveCount(2);
        activeResult.Value.Select(c => c.CloCode).Should().ContainInOrder("CLO1", "CLO3");
    }

    [Fact(DisplayName = "CLO-05: Cập nhật và Xóa CLO thành công")]
    public async Task Update_And_Delete_CourseClo_Should_Succeed()
    {
        await using var context = CreateDbContext();

        var courseId = Guid.NewGuid();
        var clo = new CourseClo
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            CloCode = "CLO1",
            Description = "Mô tả ban đầu",
            BloomLevel = BloomLevel.Understand,
            OrderIndex = 1,
            IsActive = true
        };
        context.CourseClos.Add(clo);
        await context.SaveChangesAsync();

        // Update
        var updateHandler = new UpdateCourseCloCommandHandler(context);
        var updateResult = await updateHandler.Handle(new UpdateCourseCloCommand(
            courseId,
            clo.Id,
            "CLO1_UPDATED",
            "Mô tả đã cập nhật",
            BloomLevel.Apply,
            5,
            true,
            UserRole.DepartmentHead), CancellationToken.None);

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.CloCode.Should().Be("CLO1_UPDATED");
        updateResult.Value.BloomLevel.Should().Be(BloomLevel.Apply);

        // Delete
        var deleteHandler = new DeleteCourseCloCommandHandler(context);
        var deleteResult = await deleteHandler.Handle(new DeleteCourseCloCommand(courseId, clo.Id, UserRole.DepartmentHead), CancellationToken.None);

        deleteResult.IsSuccess.Should().BeTrue();
        var remaining = await context.CourseClos.FirstOrDefaultAsync(c => c.Id == clo.Id);
        remaining.Should().BeNull();
    }
}
