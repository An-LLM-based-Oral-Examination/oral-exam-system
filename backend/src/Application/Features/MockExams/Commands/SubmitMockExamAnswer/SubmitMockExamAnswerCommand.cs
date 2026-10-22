using System;
using System.IO;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.DTOs;

namespace OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;

/// <summary>
/// Command nộp câu trả lời cho một câu hỏi trong ca thi thử (Voice-First STT - MF-02).
/// Âm thanh được stream trực tiếp qua Cloudflare Whisper AI trong bộ nhớ RAM, không lưu lên Cloudflare R2.
/// </summary>
public sealed record SubmitMockExamAnswerCommand(
    Guid SessionId,
    Guid StudentId,
    Guid QuestionId,
    Stream? AudioStream,
    string? FileName,
    string? ContentType,
    int TimeTakenSeconds,
    string? DirectTranscript = null,
    Guid? AnswerId = null
) : IRequest<Result<SubmitMockExamAnswerResponse>>;
