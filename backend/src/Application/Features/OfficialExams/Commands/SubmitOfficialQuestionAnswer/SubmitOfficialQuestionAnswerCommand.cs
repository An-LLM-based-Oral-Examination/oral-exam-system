using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.OfficialExams.DTOs;

namespace OralExamination.Application.Features.OfficialExams.Commands.SubmitOfficialQuestionAnswer;

public sealed record SubmitOfficialQuestionAnswerCommand(
    Guid ActorUserId,
    Guid TicketId,
    Guid QuestionId,
    int FollowUpIndex,
    Guid? ParentSubmissionId,
    string AudioObjectKey,
    string AudioHashSha256,
    string? InitialVoiceTranscript,
    string? TranscriptWhisper,
    string? TimestampsWhisper,
    int TimeSpentSeconds,
    bool IsManuallyEdited,
    string? DeviceId,
    string? KioskSessionToken) : IRequest<Result<SubmitOfficialQuestionAnswerResponse>>;
