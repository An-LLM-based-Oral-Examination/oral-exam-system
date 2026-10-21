using System.IO;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.QuestionBank.DTOs;

namespace OralExamination.Application.Features.QuestionBank.Commands.UploadQuestionImage;

public sealed record UploadQuestionImageCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? Description = null
) : IRequest<Result<UploadQuestionImageResponseDto>>;
