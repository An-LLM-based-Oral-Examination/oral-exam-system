using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;

public sealed class UploadAudioPracticeCommandHandler : IRequestHandler<UploadAudioPracticeCommand, Result<UploadAudioPracticeResponse>>
{
    private readonly ISpeechToTextService _speechToTextService;

    public UploadAudioPracticeCommandHandler(ISpeechToTextService speechToTextService)
    {
        _speechToTextService = speechToTextService;
    }

    public async Task<Result<UploadAudioPracticeResponse>> Handle(UploadAudioPracticeCommand request, CancellationToken cancellationToken)
    {
        if (request.FileStream == null)
        {
            return Result<UploadAudioPracticeResponse>.Failure("File ghi âm không hợp lệ hoặc rỗng.");
        }

        if (request.FileStream.CanSeek && request.FileStream.Length == 0)
        {
            return Result<UploadAudioPracticeResponse>.Failure("File ghi âm không hợp lệ hoặc rỗng.");
        }

        Stream streamToTranscribe = request.FileStream;
        System.IO.MemoryStream? bufferStream = null;

        try
        {
            if (!request.FileStream.CanSeek)
            {
                bufferStream = new System.IO.MemoryStream();
                await request.FileStream.CopyToAsync(bufferStream, cancellationToken);
                if (bufferStream.Length == 0)
                {
                    return Result<UploadAudioPracticeResponse>.Failure("File ghi âm không hợp lệ hoặc rỗng.");
                }
                bufferStream.Position = 0;
                streamToTranscribe = bufferStream;
            }
            else if (request.FileStream.Position != 0)
            {
                request.FileStream.Position = 0;
            }

            // Bóc băng Whisper STT trực tiếp dạng stream (không lưu R2, không hash SHA-256 cho MF-01/02)
            var transcript = await _speechToTextService.TranscribeAudioAsync(streamToTranscribe, request.FileName, cancellationToken);

            var response = new UploadAudioPracticeResponse(
                Transcript: transcript
            );

            return Result<UploadAudioPracticeResponse>.Success(response);
        }
        catch (System.Exception ex)
        {
            return Result<UploadAudioPracticeResponse>.Failure($"Lỗi khi bóc băng âm thanh: {ex.Message}");
        }
        finally
        {
            bufferStream?.Dispose();
        }
    }
}
