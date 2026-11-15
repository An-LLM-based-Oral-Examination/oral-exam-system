using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.InClassShifts.Commands.DeleteInClassShift;
using OralExamination.Application.Features.InClassShifts.Commands.UpdateInClassShift;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Persistence;
using Xunit;

namespace OralExamination.UnitTests.Features.InClassShifts;

public class InClassShiftMutationTests
{
    private static async Task<(OralExamDbContext Db, InClassExamShift Shift, User Actor)> Seed()
    {
        var db = new OralExamDbContext(new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var actor = new User { Email = "owner@test.local", FullName = "Owner", Role = UserRole.Lecturer, IsActive = true };
        var course = new Course { Code = "C", Name = "Course", IsActive = true };
        var cls = new Class { Code = "CL", Name = "Class", Course = course, CourseId = course.Id, LecturerId = actor.Id, IsActive = true };
        var shift = new InClassExamShift { Class = cls, ClassId = cls.Id, LecturerId = actor.Id,
            ShiftName = "Before", TotpSecretKey = "preserve-secret", StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1) };
        db.Users.Add(actor);
        db.InClassExamShifts.Add(shift);
        await db.SaveChangesAsync();
        return (db, shift, actor);
    }

    [Fact]
    public async Task Delete_EmptyFutureScheduledShift_RemovesIt()
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        var result = await new DeleteInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id), default);
        result.IsSuccess.Should().BeTrue();
        (await db.InClassExamShifts.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData("started")]
    [InlineData("status")]
    [InlineData("locked")]
    [InlineData("finalized")]
    [InlineData("participant")]
    [InlineData("password")]
    [InlineData("submission")]
    public async Task Delete_ProtectedShift_ReportsSpecificReasonAndPreservesData(string scenario)
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        var expected = scenario switch
        {
            "started" => ShiftMutationErrors.AlreadyStarted,
            "status" => ShiftMutationErrors.NotScheduled,
            "locked" => ShiftMutationErrors.Locked,
            "finalized" => ShiftMutationErrors.Finalized,
            "participant" => ShiftMutationErrors.HasParticipants,
            "password" => ShiftMutationErrors.HasPasswords,
            _ => ShiftMutationErrors.HasSubmissions
        };
        switch (scenario)
        {
            case "started": shift.StartTime = DateTime.UtcNow.AddMinutes(-1); break;
            case "status": shift.Status = "IN_PROGRESS"; break;
            case "locked": shift.IsLocked = true; break;
            case "finalized": shift.FinalizedAt = DateTime.UtcNow; break;
            case "participant": db.InClassShiftParticipants.Add(new() { ShiftId = shift.Id, StudentId = actor.Id }); break;
            case "password": db.InClassShiftPasswords.Add(new() { ShiftId = shift.Id, PlainCode = "123456", PasswordHash = "hash" }); break;
            case "submission": db.InClassExamSubmissions.Add(new() { ShiftId = shift.Id, StudentId = actor.Id, Transcript = "answer" }); break;
        }
        await db.SaveChangesAsync();
        var result = await new DeleteInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id), default);
        result.Error.Should().Be(expected);
        (await db.InClassExamShifts.AnyAsync(x => x.Id == shift.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.Student, true)]
    [InlineData(UserRole.Lecturer, false)]
    [InlineData(UserRole.DepartmentHead, true)]
    public async Task Delete_UnauthorizedActor_IsRejected(string role, bool active)
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        actor.Role = role;
        actor.IsActive = active;
        await db.SaveChangesAsync();
        var result = await new DeleteInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id), default);
        result.Error.Should().Be(InClassShiftErrors.LecturerNotAuthorized);
    }

    [Fact]
    public async Task Update_SelectedApprovedQuestion_PreservesIdentityAndSecret()
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        var question = new PracticeQuestion { CourseId = shift.Class.CourseId, Title = "Title", Content = "Question", IsActive = true, ApprovalStatus = ApprovalStatus.Approved };
        db.PracticeQuestions.Add(question);
        await db.SaveChangesAsync();
        var result = await new UpdateInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id,
            "After", shift.StartTime.AddHours(1), shift.EndTime.AddHours(1), QuestionPoolMode: "SELECTED_POOL", SelectedQuestionIds: [question.Id]), default);
        result.IsSuccess.Should().BeTrue();
        shift.ShiftName.Should().Be("After");
        shift.LecturerId.Should().Be(actor.Id);
        shift.TotpSecretKey.Should().Be("preserve-secret");
    }

    [Fact]
    public async Task Update_InvalidQuestion_DoesNotChangeShift()
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        var result = await new UpdateInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id,
            "After", shift.StartTime, shift.EndTime, QuestionPoolMode: "SELECTED_POOL", SelectedQuestionIds: [Guid.NewGuid()]), default);
        result.Error.Should().Be(InClassShiftErrors.InvalidCourseQuestion);
        shift.ShiftName.Should().Be("Before");
    }

    [Fact]
    public async Task Update_ExistingParticipant_PreservesConfig()
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        db.InClassShiftParticipants.Add(new() { ShiftId = shift.Id, StudentId = actor.Id });
        await db.SaveChangesAsync();
        var result = await new UpdateInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id,
            "After", shift.StartTime, shift.EndTime), default);
        result.Error.Should().Be(ShiftMutationErrors.HasParticipants);
        shift.ShiftName.Should().Be("Before");
    }

    [Fact]
    public async Task Update_NewStartInPast_IsRejectedWithoutChanges()
    {
        var (db, shift, actor) = await Seed();
        await using var dispose = db;
        var result = await new UpdateInClassShiftCommandHandler(db).Handle(new(actor.Id, shift.Id,
            "After", DateTime.UtcNow.AddMinutes(-1), shift.EndTime), default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("tương lai");
        shift.ShiftName.Should().Be("Before");
    }
}
