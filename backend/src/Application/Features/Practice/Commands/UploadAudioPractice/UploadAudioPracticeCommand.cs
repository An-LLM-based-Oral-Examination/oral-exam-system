using MediatR;
using OralExamination.Application.Common.Models;
using System.IO;

namespace OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;

public sealed record UploadAudioPracticeCommand(
    Stream FileStream,
    string FileName,
    string ContentType
) : IRequest<Result<UploadAudioPracticeResponse>>;

public sealed record UploadAudioPracticeResponse(
    string Transcript
);
