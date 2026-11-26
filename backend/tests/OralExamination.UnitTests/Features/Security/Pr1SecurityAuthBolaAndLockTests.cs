using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using Google.Apis.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OralExamination.API.Configuration;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.Commands.AssignAppealLecturer;
using OralExamination.Application.Features.Appeals.Commands.BatchAssignAppeals;
using OralExamination.Application.Features.Appeals.Commands.CreateAppeal;
using OralExamination.Application.Features.Appeals.DTOs;
using OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;
using OralExamination.Application.Features.InClassShifts.Commands.FinalizeInClassShiftGrades;
using OralExamination.Application.Features.OfficialExams;
using OralExamination.Application.Features.OfficialExams.Commands.CreateExamSet;
using OralExamination.Application.Features.OfficialExams.Commands.SubmitExamSetForReview;
using OralExamination.Application.Features.OfficialExams.DTOs;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamSets;
using OralExamination.Application.Features.OfficialExams.Queries.GetExamShifts;
using OralExamination.Application.Features.OfficialExams.Queries.GetMyWorkstationTicket;
using OralExamination.Application.Features.OfficialExams.Queries.GetOfficialExamSessions;
using OralExamination.Application.Features.QuestionBank.Commands.CreateRubric;
using OralExamination.Application.Features.QuestionBank.DTOs;
using OralExamination.Application.Features.QuestionBank.Queries.GetRubrics;
using OralExamination.Application.Features.Semesters.Queries.GetAcademicClasses;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Domain.Exceptions;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Persistence.Interceptors;
using Xunit;

namespace OralExamination.UnitTests.Features.Security;

public sealed class Pr1SecurityAuthBolaAndLockTests
{
    private static OralExamDbContext CreateDbContext(bool withOneWayLockInterceptor = false)
    {
        var builder = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase($"pr1-sec-{Guid.NewGuid()}");

        if (withOneWayLockInterceptor)
        {
            builder.AddInterceptors(new OneWayLockInterceptor());
        }

        return new OralExamDbContext(builder.Options);
    }

    private static User CreateUser(string role, string prefix, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{prefix}-{Guid.NewGuid():N}@fpt.edu.vn",
        FullName = $"{prefix} User",
        Role = role,
        IsActive = isActive
    };

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role) => new(
        new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    [Fact(DisplayName = "PR1-01: OneWayLockInterceptor chặn Modify/Delete InClassShiftParticipant sau khi ca MF-05 đã khóa (FINALIZED) nhưng vẫn cho phép bước chốt sổ ban đầu")]
    public async Task OneWayLockInterceptor_BlocksParticipantMutationAfterShiftLocked_AllowsInitialFinalize()
    {
        await using var db = CreateDbContext(withOneWayLockInterceptor: true);

        var lecturer = CreateUser(UserRole.Lecturer, "mf05-lec");
        var student = CreateUser(UserRole.Student, "mf05-sv");
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", IsActive = true };
        var cls = new Class
        {
            Id = Guid.NewGuid(),
            Code = "SE1801",
            CourseId = course.Id,
            LecturerId = lecturer.Id,
            IsActive = true
        };
        var shift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca MF-05",
            TotpSecretKey = "SECRETKEY123456",
            Status = "CLOSED",
            IsLocked = false,
            TotalQuestionsPerStudent = 1,
            ExamDurationMinutes = 15,
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow
        };
        var question = new PracticeQuestion
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Title = "Q1",
            Content = "Explain DI",
            Difficulty = "medium",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true
        };
        var participant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            AssignedQuestionId = question.Id,
            Status = "GRADED",
            TotalFinalScore = 8.00m,
            JoinedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var submission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            QuestionId = question.Id,
            Transcript = "Dependency injection decouples components.",
            AiScore = 8.00m,
            FinalScore = 8.00m,
            Status = "GRADED",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        db.Users.AddRange(lecturer, student);
        db.Courses.Add(course);
        db.Classes.Add(cls);
        db.InClassExamShifts.Add(shift);
        db.PracticeQuestions.Add(question);
        db.InClassShiftParticipants.Add(participant);
        db.InClassExamSubmissions.Add(submission);
        await db.SaveChangesAsync();

        // 1. Initial finalize transitions shift.IsLocked = true and participant.Status = FINALIZED in the same transaction
        var finalizeHandler = new FinalizeInClassShiftGradesCommandHandler(db);
        var finalizeResult = await finalizeHandler.Handle(
            new FinalizeInClassShiftGradesCommand(lecturer.Id, shift.Id),
            CancellationToken.None);
        finalizeResult.IsSuccess.Should().BeTrue();

        var lockedParticipant = await db.InClassShiftParticipants.FirstAsync(p => p.Id == participant.Id);
        lockedParticipant.Status.Should().Be("FINALIZED");

        // 2. Post-lock modification of participant score/status MUST throw OneWayLockException
        lockedParticipant.TotalFinalScore = 10.00m;
        var modifyAct = () => db.SaveChangesAsync(CancellationToken.None);
        await modifyAct.Should().ThrowAsync<OneWayLockException>();

        // 3. Re-pointing ShiftId to another unlocked shift MUST also be caught via OriginalValue check
        db.Entry(lockedParticipant).Reload();
        var otherShift = new InClassExamShift
        {
            Id = Guid.NewGuid(),
            ClassId = cls.Id,
            LecturerId = lecturer.Id,
            ShiftName = "Ca Unlocked",
            TotpSecretKey = "SECRETKEY654321",
            Status = "ACTIVE",
            IsLocked = false,
            TotalQuestionsPerStudent = 2,
            ExamDurationMinutes = 15,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };
        db.InClassExamShifts.Add(otherShift);
        await db.SaveChangesAsync();

        lockedParticipant.ShiftId = otherShift.Id;
        var repointAct = () => db.SaveChangesAsync(CancellationToken.None);
        await repointAct.Should().ThrowAsync<OneWayLockException>();

        // 4. Deleting participant on a locked shift MUST throw OneWayLockException
        db.Entry(lockedParticipant).Reload();
        db.InClassShiftParticipants.Remove(lockedParticipant);
        var deleteAct = () => db.SaveChangesAsync(CancellationToken.None);
        await deleteAct.Should().ThrowAsync<OneWayLockException>();

        // 5. Inserting (EntityState.Added) a new participant or submission into an already-locked shift MUST throw OneWayLockException
        db.Entry(lockedParticipant).State = EntityState.Unchanged;
        var extraStudent = CreateUser(UserRole.Student, "mf05-sv-late");
        db.Users.Add(extraStudent);
        await db.SaveChangesAsync();

        var lateParticipant = new InClassShiftParticipant
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = extraStudent.Id,
            Status = "JOINED",
            JoinedAt = DateTime.UtcNow
        };
        db.InClassShiftParticipants.Add(lateParticipant);
        var addParticipantAct = () => db.SaveChangesAsync(CancellationToken.None);
        await addParticipantAct.Should().ThrowAsync<OneWayLockException>();

        db.Entry(lateParticipant).State = EntityState.Detached;
        var lateSubmission = new InClassExamSubmission
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = extraStudent.Id,
            QuestionId = question.Id,
            Transcript = "Late injection",
            AiScore = 9.0m,
            FinalScore = 9.0m,
            Status = "GRADED",
            SubmittedAt = DateTime.UtcNow
        };
        db.InClassExamSubmissions.Add(lateSubmission);
        var addSubmissionAct = () => db.SaveChangesAsync(CancellationToken.None);
        await addSubmissionAct.Should().ThrowAsync<OneWayLockException>();

        // 6. Disconnected Attach with forged ShiftId pointing to an unlocked shift MUST still be blocked by DB lookup
        db.Entry(lateSubmission).State = EntityState.Detached;
        db.Entry(lockedParticipant).State = EntityState.Detached;
        var forgedDetachedParticipant = new InClassShiftParticipant
        {
            Id = participant.Id,
            ShiftId = otherShift.Id,
            StudentId = student.Id,
            Status = "GRADED",
            TotalFinalScore = 10.00m
        };
        db.InClassShiftParticipants.Update(forgedDetachedParticipant);
        var disconnectedAct = () => db.SaveChangesAsync(CancellationToken.None);
        await disconnectedAct.Should().ThrowAsync<OneWayLockException>();
    }

    [Fact(DisplayName = "PR1-02: CreateExamSet, SubmitExamSetForReview và GetExamSets chặn BOLA chéo môn/chéo GV/chéo Trưởng BM và cho phép đúng chủ sở hữu")]
    public async Task ExamSets_EnforceCourseAndCreatorScoping_FailClosed()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "head-se");
        var headB = CreateUser(UserRole.DepartmentHead, "head-ent");
        var lecA = CreateUser(UserRole.Lecturer, "lec-se");
        var lecA2 = CreateUser(UserRole.Lecturer, "lec-se-other");
        var lecB = CreateUser(UserRole.Lecturer, "lec-ent");
        var officer = CreateUser(UserRole.ExaminationOfficer, "officer");

        var progA = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "SE", DepartmentHeadUserId = headA.Id, IsActive = true };
        var progB = new AcademicProgram { Id = Guid.NewGuid(), Code = "ENT", Name = "ENT", DepartmentHeadUserId = headB.Id, IsActive = true };
        var courseA = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "Architecture", AcademicProgramId = progA.Id, IsActive = true };
        var structureA = new ExamStructure { Id = Guid.NewGuid(), CourseId = courseA.Id, Name = "Struct A", TotalQuestions = 1, IsActive = true };
        var questionA = new ExamQuestion { Id = Guid.NewGuid(), CourseId = courseA.Id, RubricId = Guid.NewGuid(), Title = "Q1", Content = "Content", ApprovalStatus = ApprovalStatus.Draft, IsActive = true };

        db.Users.AddRange(headA, headB, lecA, lecA2, lecB, officer);
        db.AcademicPrograms.AddRange(progA, progB);
        db.Courses.Add(courseA);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseA.Id, LecturerUserId = lecA.Id, AssignedByUserId = headA.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseA.Id, LecturerUserId = lecA2.Id, AssignedByUserId = headA.Id, IsActive = true });
        db.ExamStructures.Add(structureA);
        db.ExamQuestions.Add(questionA);
        await db.SaveChangesAsync();

        var createHandler = new CreateExamSetCommandHandler(db);
        var submitHandler = new SubmitExamSetForReviewCommandHandler(db);
        var getHandler = new GetExamSetsQueryHandler(db);

        // Foreign lecturer & foreign dept head MUST be rejected on CreateExamSet
        (await createHandler.Handle(new CreateExamSetCommand(structureA.Id, courseA.Id, null, "SET-LECB", [questionA.Id], lecB.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await createHandler.Handle(new CreateExamSetCommand(structureA.Id, courseA.Id, null, "SET-HEADB", [questionA.Id], headB.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);

        // Assigned lecturer A creates draft set
        var createdByLecA = await createHandler.Handle(
            new CreateExamSetCommand(structureA.Id, courseA.Id, null, "SET-LECA", [questionA.Id], lecA.Id, UserRole.Lecturer),
            CancellationToken.None);
        createdByLecA.IsSuccess.Should().BeTrue();

        // Another lecturer on same course (lecA2), foreign lecturer (lecB), and foreign head (headB) CANNOT submit lecA's draft set
        (await submitHandler.Handle(new SubmitExamSetForReviewCommand(createdByLecA.Value.Id, lecA2.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await submitHandler.Handle(new SubmitExamSetForReviewCommand(createdByLecA.Value.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await submitHandler.Handle(new SubmitExamSetForReviewCommand(createdByLecA.Value.Id, headB.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);

        // Creator lecA CAN submit own set
        (await submitHandler.Handle(new SubmitExamSetForReviewCommand(createdByLecA.Value.Id, lecA.Id, UserRole.Lecturer), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        // GetExamSets: fail-closed for missing actor, missing/mismatched role, foreign lecturer, foreign dept head
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, lecA.Id, null), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, lecA.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, lecB.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, headB.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);

        // Horizontal Lecturer BOLA: lecA2 (same course) CANNOT read lecA's exam set
        var lecA2Sets = await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, lecA2.Id, UserRole.Lecturer), CancellationToken.None);
        lecA2Sets.IsSuccess.Should().BeTrue();
        lecA2Sets.Value.Should().BeEmpty();

        // Creator lecA and owning DepartmentHead headA CAN see SUBMITTED_FOR_REVIEW set
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, lecA.Id, UserRole.Lecturer), CancellationToken.None))
            .Value.Should().ContainSingle(es => es.Id == createdByLecA.Value.Id);
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, headA.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Value.Should().ContainSingle(es => es.Id == createdByLecA.Value.Id);

        // Academic vs Exam Office boundary: ExaminationOfficer CANNOT see un-handed-over sets (DRAFT / SUBMITTED_FOR_REVIEW / OFFICIAL)
        var officerBeforeHandOver = await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        officerBeforeHandOver.IsSuccess.Should().BeTrue();
        officerBeforeHandOver.Value.Should().BeEmpty();
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, ExamSetStatus.SubmittedForReview, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);
        (await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, ExamSetStatus.Draft, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None))
            .Error.Should().Be(OfficialExamErrors.Forbidden);

        // After Department Head hands over the set -> ExaminationOfficer CAN read HANDED_OVER set
        var storedSet = await db.ExamSets.FirstAsync(es => es.Id == createdByLecA.Value.Id);
        storedSet.Status = ExamSetStatus.HandedOver;
        storedSet.HandedOverAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var officerAfterHandOver = await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, ExamSetStatus.HandedOver, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        officerAfterHandOver.IsSuccess.Should().BeTrue();
        officerAfterHandOver.Value.Should().ContainSingle(es => es.Id == createdByLecA.Value.Id);
        var officerUnfilteredAfterHandOver = await getHandler.Handle(new GetExamSetsQuery(courseA.Id, null, null, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        officerUnfilteredAfterHandOver.IsSuccess.Should().BeTrue();
        officerUnfilteredAfterHandOver.Value.Should().ContainSingle(es => es.Id == createdByLecA.Value.Id);
    }

    [Fact(DisplayName = "PR1-03: GetOfficialExamSessions và GetExamShifts cô lập dữ liệu theo vai trò và ẩn CertificateHash với Proctor/GV không phải Giám khảo")]
    public async Task OfficialExamSessionsAndShifts_EnforceRoleScoping_AndMaskCertificateHash()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "head-a");
        var headB = CreateUser(UserRole.DepartmentHead, "head-b");
        var examinerA = CreateUser(UserRole.Lecturer, "examiner-a");
        var proctorLecturer = CreateUser(UserRole.Lecturer, "proctor-lec");
        var proctor1 = CreateUser(UserRole.Proctor, "proctor-1");
        var proctor2 = CreateUser(UserRole.Proctor, "proctor-2");
        var officer = CreateUser(UserRole.ExaminationOfficer, "officer");

        var progA = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "SE", DepartmentHeadUserId = headA.Id, IsActive = true };
        var progB = new AcademicProgram { Id = Guid.NewGuid(), Code = "ENT", Name = "ENT", DepartmentHeadUserId = headB.Id, IsActive = true };
        var courseA = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", AcademicProgramId = progA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "ENT104", Name = "ENT", AcademicProgramId = progB.Id, IsActive = true };

        var sessionA = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseA.Id, Title = "Session SE", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = "scheduled" };
        var sessionB = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = courseB.Id, Title = "Session ENT", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = "scheduled" };

        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var shiftA = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = sessionA.Id,
            ShiftName = "Shift SE",
            RoomLab = "LAB-1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            ExaminerUserId = examinerA.Id,
            ProctorUserId = proctor1.Id,
            Status = "scheduled",
            CertificateHash = hashA
        };
        var shiftB = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = sessionB.Id,
            ShiftName = "Shift ENT",
            RoomLab = "LAB-2",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            ExaminerUserId = null,
            ProctorUserId = proctorLecturer.Id,
            Status = "scheduled",
            CertificateHash = hashB
        };

        db.Users.AddRange(headA, headB, examinerA, proctorLecturer, proctor1, proctor2, officer);
        db.AcademicPrograms.AddRange(progA, progB);
        db.Courses.AddRange(courseA, courseB);
        db.OfficialExamSessions.AddRange(sessionA, sessionB);
        db.RealExamSessionShifts.AddRange(shiftA, shiftB);
        await db.SaveChangesAsync();

        var sessionHandler = new GetOfficialExamSessionsQueryHandler(db);
        var shiftHandler = new GetExamShiftsQueryHandler(db);

        // Missing actor fails closed
        (await sessionHandler.Handle(new GetOfficialExamSessionsQuery(), CancellationToken.None)).Error.Should().Be(OfficialExamErrors.Forbidden);
        (await shiftHandler.Handle(new GetExamShiftsQuery(), CancellationToken.None)).Error.Should().Be(OfficialExamErrors.Forbidden);

        // Department Head A sees only Session A / Shift A (with CertificateHash)
        var headASessions = await sessionHandler.Handle(new GetOfficialExamSessionsQuery(ActorUserId: headA.Id, ActorRole: UserRole.DepartmentHead), CancellationToken.None);
        headASessions.Value.Should().ContainSingle(s => s.Id == sessionA.Id);
        var headAShifts = await shiftHandler.Handle(new GetExamShiftsQuery(ActorUserId: headA.Id, ActorRole: UserRole.DepartmentHead), CancellationToken.None);
        headAShifts.Value.Should().ContainSingle(s => s.Id == shiftA.Id && s.CertificateHash == hashA);

        // Proctor 1 sees ONLY Shift A, NEVER CertificateHash, and CANNOT query proctorLecturer's shift
        var proctor1Shifts = await shiftHandler.Handle(new GetExamShiftsQuery(ActorUserId: proctor1.Id, ActorRole: UserRole.Proctor), CancellationToken.None);
        proctor1Shifts.Value.Should().ContainSingle(s => s.Id == shiftA.Id && s.CertificateHash == null);
        var proctor1Spoof = await shiftHandler.Handle(new GetExamShiftsQuery(ProctorUserId: proctorLecturer.Id, ActorUserId: proctor1.Id, ActorRole: UserRole.Proctor), CancellationToken.None);
        proctor1Spoof.Value.Should().BeEmpty();
        var proctor2Shifts = await shiftHandler.Handle(new GetExamShiftsQuery(ActorUserId: proctor2.Id, ActorRole: UserRole.Proctor), CancellationToken.None);
        proctor2Shifts.Value.Should().BeEmpty();

        // Examiner Lecturer sees Shift A WITH CertificateHash
        var examinerShifts = await shiftHandler.Handle(new GetExamShiftsQuery(ActorUserId: examinerA.Id, ActorRole: UserRole.Lecturer), CancellationToken.None);
        examinerShifts.Value.Should().ContainSingle(s => s.Id == shiftA.Id && s.CertificateHash == hashA);

        // Lecturer assigned only as Proctor sees Shift B with CertificateHash MASKED (null)
        var proctorLecShifts = await shiftHandler.Handle(new GetExamShiftsQuery(ActorUserId: proctorLecturer.Id, ActorRole: UserRole.Lecturer), CancellationToken.None);
        proctorLecShifts.Value.Should().ContainSingle(s => s.Id == shiftB.Id && s.CertificateHash == null);

        // Examination Officer sees ALL sessions and shifts with CertificateHash
        var officerShifts = await shiftHandler.Handle(new GetExamShiftsQuery(ActorUserId: officer.Id, ActorRole: UserRole.ExaminationOfficer), CancellationToken.None);
        officerShifts.Value.Should().HaveCount(2);
        officerShifts.Value.Should().OnlyContain(s => !string.IsNullOrEmpty(s.CertificateHash));
    }

    [Fact(DisplayName = "PR1-04: Rubrics chặn BOLA tạo/xem theo courseId & rubricId và RubricsController ánh xạ FORBIDDEN -> 403")]
    public async Task Rubrics_EnforceCourseScoping_AndControllerMapsForbiddenTo403()
    {
        await using var db = CreateDbContext();

        var headA = CreateUser(UserRole.DepartmentHead, "rubric-head-a");
        var lecA = CreateUser(UserRole.Lecturer, "rubric-lec-a");
        var lecB = CreateUser(UserRole.Lecturer, "rubric-lec-b");
        var courseA = new Course { Id = Guid.NewGuid(), Code = "PRN231", Name = "PRN231", DepartmentHeadUserId = headA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "ENT104", Name = "ENT104", IsActive = true };

        db.Users.AddRange(headA, lecA, lecB);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.AddRange(
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseA.Id, LecturerUserId = lecA.Id, AssignedByUserId = headA.Id, IsActive = true },
            new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseB.Id, LecturerUserId = lecB.Id, AssignedByUserId = headA.Id, IsActive = true });
        await db.SaveChangesAsync();

        var criteria = new List<RubricCriterionDraftDto>
        {
            new("C1", 5.00m, 0.5m, "Understand", "Desc 1", 1),
            new("C2", 5.00m, 0.5m, "Apply", "Desc 2", 2)
        };
        var createHandler = new CreateRubricCommandHandler(db);
        var getHandler = new GetRubricsQueryHandler(db);

        // Lecturer B CANNOT create rubric for Course A
        var crossCreate = await createHandler.Handle(new CreateRubricCommand(lecB.Id, courseA.Id, "Hack Rubric", null, criteria), CancellationToken.None);
        crossCreate.IsSuccess.Should().BeFalse();
        crossCreate.Error.Should().StartWith("FORBIDDEN:");

        // Lecturer A creates rubric on Course A
        var validCreate = await createHandler.Handle(new CreateRubricCommand(lecA.Id, courseA.Id, "Rubric SE", null, criteria), CancellationToken.None);
        validCreate.IsSuccess.Should().BeTrue();

        // Lecturer B CANNOT read Course A rubrics by courseId OR by rubricId, and unfiltered list returns empty
        (await getHandler.Handle(new GetRubricsQuery(courseA.Id, null, lecA.Id, null), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await getHandler.Handle(new GetRubricsQuery(courseA.Id, null, lecA.Id, UserRole.DepartmentHead), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await getHandler.Handle(new GetRubricsQuery(courseA.Id, null, lecB.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await getHandler.Handle(new GetRubricsQuery(null, validCreate.Value.Id, lecB.Id, UserRole.Lecturer), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await getHandler.Handle(new GetRubricsQuery(null, null, lecB.Id, UserRole.Lecturer), CancellationToken.None))
            .Value.Should().BeEmpty();

        // RubricsController maps FORBIDDEN -> 403 on GetRubrics, GetRubricById, and CreateRubric
        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<GetRubricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<RubricDetailDto>>.Failure("FORBIDDEN: No access"));
        senderMock.Setup(s => s.Send(It.IsAny<CreateRubricCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateRubricResponseDto>.Failure("FORBIDDEN: No access"));

        var controller = new RubricsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(lecB.Id, UserRole.Lecturer) }
            }
        };

        (await controller.GetRubrics(courseA.Id)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await controller.GetRubricById(validCreate.Value.Id)).Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await controller.CreateRubric(new CreateRubricRequestDto(courseA.Id, "X", null, criteria)))
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact(DisplayName = "PR1-05: SemestersController/GetAcademicClasses & ExamStructuresController khóa RBAC chặt và che JoinCode đúng vai trò")]
    public async Task AcademicClassesAndExamStructures_EnforceRbacAndMaskJoinCodes()
    {
        // 1. Attribute verification: student and proctor are excluded from both endpoints
        var getClassesAttr = typeof(SemestersController)
            .GetMethod(nameof(SemestersController.GetAcademicClasses))!
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        getClassesAttr.Should().NotBeNull();
        getClassesAttr!.Roles.Should().Be("admin,department_head,lecturer,examination_officer");

        var examStructuresAttr = typeof(ExamStructuresController)
            .GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        examStructuresAttr.Should().NotBeNull();
        examStructuresAttr!.Roles.Should().Be("admin,department_head,lecturer,examination_officer");

        // 2. Handler verification: role scoping + JoinCode masking
        await using var db = CreateDbContext();
        var admin = CreateUser(UserRole.Admin, "cls-admin");
        var headA = CreateUser(UserRole.DepartmentHead, "cls-head-a");
        var lec1 = CreateUser(UserRole.Lecturer, "cls-lec-1");
        var lec2 = CreateUser(UserRole.Lecturer, "cls-lec-2");
        var officer = CreateUser(UserRole.ExaminationOfficer, "cls-officer");
        var student = CreateUser(UserRole.Student, "cls-student");

        var semester = new Semester { Id = Guid.NewGuid(), Code = "FA26", Name = "Fall 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var courseA = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD", SemesterId = semester.Id, DepartmentHeadUserId = headA.Id, IsActive = true };
        var courseB = new Course { Id = Guid.NewGuid(), Code = "ENT104", Name = "ENT", SemesterId = semester.Id, IsActive = true };

        var classA1 = new Class { Id = Guid.NewGuid(), Code = "SE1801", CourseId = courseA.Id, SemesterId = semester.Id, LecturerId = lec1.Id, JoinCode = "SECRET-A1", IsJoinEnabled = true, IsActive = true };
        var classA2 = new Class { Id = Guid.NewGuid(), Code = "SE1802", CourseId = courseA.Id, SemesterId = semester.Id, LecturerId = lec2.Id, JoinCode = "SECRET-A2", IsJoinEnabled = true, IsActive = true };
        var classA3Disabled = new Class { Id = Guid.NewGuid(), Code = "SE1803", CourseId = courseA.Id, SemesterId = semester.Id, LecturerId = lec1.Id, JoinCode = "SECRET-A3", IsJoinEnabled = false, IsActive = true };
        var classB1 = new Class { Id = Guid.NewGuid(), Code = "ENT1801", CourseId = courseB.Id, SemesterId = semester.Id, LecturerId = lec2.Id, JoinCode = "SECRET-B1", IsJoinEnabled = true, IsActive = true };

        db.Users.AddRange(admin, headA, lec1, lec2, officer, student);
        db.Semesters.Add(semester);
        db.Courses.AddRange(courseA, courseB);
        db.CourseLecturers.Add(new CourseLecturer { Id = Guid.NewGuid(), CourseId = courseA.Id, LecturerUserId = lec1.Id, AssignedByUserId = headA.Id, IsActive = true });
        db.Classes.AddRange(classA1, classA2, classA3Disabled, classB1);
        await db.SaveChangesAsync();

        var handler = new GetAcademicClassesQueryHandler(db);

        // Student, missing actor, missing role, & mismatched role rejected with FORBIDDEN
        (await handler.Handle(new GetAcademicClassesQuery(ActorUserId: student.Id, ActorRole: UserRole.Student), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new GetAcademicClassesQuery(), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new GetAcademicClassesQuery(ActorUserId: lec1.Id, ActorRole: null), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");
        (await handler.Handle(new GetAcademicClassesQuery(ActorUserId: lec1.Id, ActorRole: UserRole.Admin), CancellationToken.None))
            .Error.Should().StartWith("FORBIDDEN:");

        // ExaminationOfficer sees classes for staff discovery, NEVER JoinCode
        var officerRes = await handler.Handle(new GetAcademicClassesQuery(ActorUserId: officer.Id, ActorRole: UserRole.ExaminationOfficer), CancellationToken.None);
        officerRes.IsSuccess.Should().BeTrue();
        officerRes.Value.Should().HaveCount(4);
        officerRes.Value.Should().OnlyContain(c => c.JoinCode == string.Empty);

        // Lecturer 1 sees Course A classes only; JoinCode visible ONLY on own enabled class SE1801, masked on SE1802 and disabled SE1803
        var lec1Res = await handler.Handle(new GetAcademicClassesQuery(ActorUserId: lec1.Id, ActorRole: UserRole.Lecturer), CancellationToken.None);
        lec1Res.IsSuccess.Should().BeTrue();
        lec1Res.Value.Select(c => c.Code).Should().BeEquivalentTo(["SE1801", "SE1802", "SE1803"]);
        lec1Res.Value.Single(c => c.Code == "SE1801").JoinCode.Should().Be("SECRET-A1");
        lec1Res.Value.Single(c => c.Code == "SE1802").JoinCode.Should().BeEmpty();
        lec1Res.Value.Single(c => c.Code == "SE1803").JoinCode.Should().BeEmpty();

        // Department Head A sees only Course A classes, disabled class JoinCode masked
        var headRes = await handler.Handle(new GetAcademicClassesQuery(ActorUserId: headA.Id, ActorRole: UserRole.DepartmentHead), CancellationToken.None);
        headRes.IsSuccess.Should().BeTrue();
        headRes.Value.Select(c => c.Code).Should().BeEquivalentTo(["SE1801", "SE1802", "SE1803"]);
        headRes.Value.Single(c => c.Code == "SE1803").JoinCode.Should().BeEmpty();

        // Admin sees all 4 classes, disabled class JoinCode masked
        var adminRes = await handler.Handle(new GetAcademicClassesQuery(ActorUserId: admin.Id, ActorRole: UserRole.Admin), CancellationToken.None);
        adminRes.IsSuccess.Should().BeTrue();
        adminRes.Value.Should().HaveCount(4);
        adminRes.Value.Single(c => c.Code == "SE1803").JoinCode.Should().BeEmpty();
    }

    [Fact(DisplayName = "PR1-06: Các controller thi thật bỏ qua X-Forwarded-For giả mạo trực tiếp và chỉ tin Connection.RemoteIpAddress")]
    public async Task OfficialExamControllers_IgnoreSpoofedXForwardedForHeader_UseConnectionRemoteIp()
    {
        var studentId = Guid.NewGuid();
        string? capturedCheckInIp = null;
        string? capturedSubmitIp = null;

        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<GetMyWorkstationTicketQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<StudentWorkstationTicketResponse>>, CancellationToken>((req, _) =>
            {
                capturedCheckInIp = ((GetMyWorkstationTicketQuery)req).RemoteIpAddress;
            })
            .ReturnsAsync(Result<StudentWorkstationTicketResponse>.Failure(OfficialExamErrors.WorkstationTicketNotFound));

        senderMock
            .Setup(s => s.Send(It.IsAny<OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam.SubmitOfficialExamCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<SubmitOfficialExamResponse>>, CancellationToken>((req, _) =>
            {
                capturedSubmitIp = ((OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialExam.SubmitOfficialExamCommand)req).RemoteIpAddress;
            })
            .ReturnsAsync(Result<SubmitOfficialExamResponse>.Failure(OfficialExamErrors.TicketNotFound));

        var httpContext = new DefaultHttpContext
        {
            User = CreatePrincipal(studentId, UserRole.Student)
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
        httpContext.Request.Headers["X-Forwarded-For"] = "198.51.100.99, 1.2.3.4";

        var checkInController = new OfficialExamCheckInController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        await checkInController.GetCurrentTicket(CancellationToken.None);
        capturedCheckInIp.Should().Be("10.20.30.40");

        var submissionsController = new OfficialExamSubmissionsController(senderMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        await submissionsController.Submit(Guid.NewGuid(), new SubmitOfficialExamRequest([]), CancellationToken.None);
        capturedSubmitIp.Should().Be("10.20.30.40");
    }

    [Fact(DisplayName = "PR1-07: AbuseProtection chặn flood Bearer token giả bằng 429 RATE_LIMITED trên bucket riêng mà không làm cạn bucket login")]
    public async Task AbuseProtection_RateLimitsFakeBearerFlood_WithoutPoisoningLoginBucket()
    {
        // Validation rejects non-positive UnauthenticatedChallengePermits
        var badConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Abuse:UnauthenticatedChallengePermits"] = "0"
            }).Build();
        Action validateBad = () => AbuseProtection.BindAndValidateOptions(badConfig);
        validateBad.Should().Throw<InvalidOperationException>();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtSigningKey"] = "pr1-bearer-flood-test-key-not-production-1234567890",
            ["Security:Abuse:UnauthenticatedChallengePermits"] = "3",
            ["Security:Abuse:LoginPermits"] = "2"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddOralExamAuthentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddApiErrorContracts();
        builder.Services.AddApiAbuseProtection(builder.Configuration);
        builder.Services.AddOralExamRateLimiting(builder.Configuration);

        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.55");
            return next(context);
        });
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseApiErrorContracts();
        app.UseAuthentication();
        app.UseApiAbuseProtection();
        app.UseAuthorization();
        app.MapPost("/api/v1/practice/submit-probe", () => Results.Ok()).RequireAuthorization();
        app.MapPost("/api/v1/auth/login-probe", () => Results.Ok());
        await app.StartAsync();

        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(url) };

        // First 3 invalid Bearer requests return 401 UNAUTHORIZED (preserving silent-refresh behavior on campus NAT)
        for (var i = 0; i < 3; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/practice/submit-probe");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"forged-jwt-{i}");
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // 4th invalid Bearer request MUST hit 429 RATE_LIMITED with Retry-After
        using var floodReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/practice/submit-probe");
        floodReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "forged-jwt-flood");
        var floodResp = await client.SendAsync(floodReq);
        floodResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        floodResp.Headers.RetryAfter.Should().NotBeNull();
        var body = JsonDocument.Parse(await floodResp.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("errorCode").GetString().Should().Be("RATE_LIMITED");

        // Login bucket MUST remain completely unaffected
        (await client.PostAsync("/api/v1/auth/login-probe", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "PR1-08: CreateAppeal gửi thông báo Trưởng BM theo AcademicProgram; Assign/BatchAssign/Proposal đều chặn GV đã hậu kiểm (LecturerAudit.LecturerId); GoogleIdTokenValidator bỏ hardcode thanhnq")]
    public async Task SecondaryFixes_AppealProgramOwner_AppealAuditConflict_AndGoogleValidatorNoHardcode()
    {
        await using var db = CreateDbContext();

        var officer = CreateUser(UserRole.ExaminationOfficer, "app-officer");
        var programHead = CreateUser(UserRole.DepartmentHead, "app-prog-head");
        var shiftExaminer = CreateUser(UserRole.Lecturer, "app-examiner");
        var ticketAuditor = CreateUser(UserRole.Lecturer, "app-auditor");
        var independentLecturer = CreateUser(UserRole.Lecturer, "app-independent");
        var student = CreateUser(UserRole.Student, "app-student");
        student.StudentCode = "SE180999";

        var program = new AcademicProgram { Id = Guid.NewGuid(), Code = "SE", Name = "SE", DepartmentHeadUserId = programHead.Id, IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "SWD392", Name = "SWD392", AcademicProgramId = program.Id, DepartmentHeadUserId = null, IsActive = true };
        var session = new OfficialExamSession { Id = Guid.NewGuid(), CourseId = course.Id, Title = "FE SWD392", ExamDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = "published" };
        var shift = new RealExamSessionShift
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            ShiftName = "Ca 1",
            RoomLab = "LAB-1",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-2),
            ExaminerUserId = shiftExaminer.Id,
            Status = "published"
        };
        var ticket = new StudentExamTicket
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            StudentId = student.Id,
            SeatNumber = 1,
            Status = ExamTicketStatus.Published,
            IsLocked = true,
            GradesPublishedAt = DateTime.UtcNow.AddHours(-1)
        };
        var audit = new LecturerAudit
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            LecturerId = ticketAuditor.Id,
            OriginalAiScore = 6.0m,
            AuditedScore = 6.5m,
            OverrideReason = "Adjusted after audio review",
            AuditedAt = DateTime.UtcNow.AddHours(-2)
        };

        db.Users.AddRange(officer, programHead, shiftExaminer, ticketAuditor, independentLecturer, student);
        db.AcademicPrograms.Add(program);
        db.Courses.Add(course);
        db.CourseLecturers.AddRange(
            new CourseLecturer { CourseId = course.Id, LecturerUserId = shiftExaminer.Id, AssignedByUserId = programHead.Id, IsActive = true },
            new CourseLecturer { CourseId = course.Id, LecturerUserId = ticketAuditor.Id, AssignedByUserId = programHead.Id, IsActive = true },
            new CourseLecturer { CourseId = course.Id, LecturerUserId = independentLecturer.Id, AssignedByUserId = programHead.Id, IsActive = true });
        db.OfficialExamSessions.Add(session);
        db.RealExamSessionShifts.Add(shift);
        db.StudentExamTickets.Add(ticket);
        db.LecturerAudits.Add(audit);
        await db.SaveChangesAsync();

        // 1. CreateAppeal notifies programHead via AcademicCourseOwnership even when course.DepartmentHeadUserId is null
        var createAppeal = await new CreateAppealCommandHandler(db)
            .Handle(new CreateAppealCommand(student.Id, ticket.Id, null, "Đề nghị chấm phúc khảo chi tiết"), CancellationToken.None);
        createAppeal.IsSuccess.Should().BeTrue();

        var headNoti = await db.Notifications.FirstOrDefaultAsync(n =>
            n.UserId == programHead.Id && n.RelatedEntityId == createAppeal.Value.Id && n.Type == "APPEAL_SUBMITTED");
        headNoti.Should().NotBeNull("Trưởng BM phụ trách ngành phải nhận thông báo phúc khảo khi course.DepartmentHeadUserId null");

        // 2. GetAppealAllocationProposal excludes BOTH shiftExaminer and ticketAuditor -> chooses independentLecturer
        var proposal = await new GetAppealAllocationProposalQueryHandler(db)
            .Handle(new GetAppealAllocationProposalQuery(session.Id, officer.Id, UserRole.ExaminationOfficer), CancellationToken.None);
        proposal.IsSuccess.Should().BeTrue();
        proposal.Value.Items.Should().ContainSingle(i =>
            i.AppealId == createAppeal.Value.Id && i.ProposedLecturerId == independentLecturer.Id);

        // 3. AssignAppealLecturer rejects ticketAuditor
        var singleAssignAuditConflict = await new AssignAppealLecturerCommandHandler(db)
            .Handle(new AssignAppealLecturerCommand(createAppeal.Value.Id, officer.Id, ticketAuditor.Id), CancellationToken.None);
        singleAssignAuditConflict.IsSuccess.Should().BeFalse();
        singleAssignAuditConflict.Error.Should().Contain("hậu kiểm");

        // 4. BatchAssignAppeals rejects ticketAuditor
        var batchAssignAuditConflict = await new BatchAssignAppealsCommandHandler(db, new BatchAssignAppealsCommandValidator())
            .Handle(new BatchAssignAppealsCommand(officer.Id, [new AppealAssignmentItemDto { AppealId = createAppeal.Value.Id, LecturerId = ticketAuditor.Id }]), CancellationToken.None);
        batchAssignAuditConflict.IsSuccess.Should().BeFalse();
        batchAssignAuditConflict.Error.Should().Contain("hậu kiểm");

        // 5. Independent lecturer assignment succeeds
        var validAssign = await new AssignAppealLecturerCommandHandler(db)
            .Handle(new AssignAppealLecturerCommand(createAppeal.Value.Id, officer.Id, independentLecturer.Id), CancellationToken.None);
        validAssign.IsSuccess.Should().BeTrue();

        // 6. GoogleIdTokenValidator no longer hardcodes thanhnq -> "Nguyễn Quang Thành"
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:GoogleClientId"] = "test-client",
                ["Authentication:GoogleAllowedDomains:0"] = "fpt.edu.vn"
            }).Build();
        var payload = new GoogleJsonWebSignature.Payload
        {
            Issuer = "https://accounts.google.com",
            Audience = "test-client",
            Subject = "sub-thanhnq",
            Email = "thanhnqse184236@fpt.edu.vn",
            EmailVerified = true,
            HostedDomain = "fpt.edu.vn",
            Name = null,
            GivenName = null,
            FamilyName = null,
            ExpirationTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(),
            Nonce = "valid-nonce"
        };
        var identity = await new GoogleIdTokenValidator(config, new StubGoogleSignatureVerifier(payload))
            .ValidateAsync("opaque-token", CancellationToken.None);
        identity.FullName.Should().Be("thanhnqse184236");
    }

    private sealed class StubGoogleSignatureVerifier(GoogleJsonWebSignature.Payload payload) : IGoogleTokenSignatureVerifier
    {
        public Task<GoogleJsonWebSignature.Payload> VerifyAsync(string idToken, string audience, CancellationToken cancellationToken)
            => Task.FromResult(payload);
    }
}
