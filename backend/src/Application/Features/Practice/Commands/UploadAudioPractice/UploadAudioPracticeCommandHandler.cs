using MediatR;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using System.IO;
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
        MemoryStream? bufferStream = null;

        try
        {
            if (!request.FileStream.CanSeek)
            {
                bufferStream = new MemoryStream();
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
            System.Exception? sttException = null;
            string? transcript = null;
            SpeechTranscriptionResult? detailed = null;

            try
            {
                detailed = await _speechToTextService.TranscribeDetailedAsync(
                    streamToTranscribe,
                    request.FileName,
                    request.VocabularyHint,
                    cancellationToken);
                transcript = detailed?.Text;

                // Chỉ dự phòng gọi TranscribeAudioAsync khi TranscribeDetailedAsync trả về null (trường hợp Moq unit test chưa cấu hình).
                // Tuyệt đối không gọi STT lần 2 khi audio im lặng/nhiễu đã được TranscribeDetailedAsync trả về chuỗi rỗng.
                if (detailed is null && string.IsNullOrWhiteSpace(transcript))
                {
                    if (streamToTranscribe.CanSeek)
                    {
                        streamToTranscribe.Position = 0;
                    }
                    transcript = await _speechToTextService.TranscribeAudioAsync(streamToTranscribe, request.FileName, cancellationToken);
                }
            }
            catch (System.Exception ex)
            {
                sttException = ex;
            }

            var normalizedServer = !string.IsNullOrWhiteSpace(transcript)
                ? TranscriptHelper.NormalizeOrFallback(transcript, _speechToTextService.NormalizeTranscript(transcript))
                : string.Empty;
            var normalizedClient = !string.IsNullOrWhiteSpace(request.ClientTranscript)
                ? TranscriptHelper.NormalizeOrFallback(request.ClientTranscript, _speechToTextService.NormalizeTranscript(request.ClientTranscript))
                : string.Empty;

            // Luôn ưu tiên tuyệt đối kết quả từ AI Server STT (Whisper / Gemini) nếu khác rỗng.
            // Tuyệt đối không để ClientTranscript ghi đè khi Server STT đã xác nhận im lặng/nhiễu.
            if (!string.IsNullOrWhiteSpace(normalizedServer))
            {
                TranscriptHelper.RegisterVerifiedTranscript(normalizedServer);
                return Result<UploadAudioPracticeResponse>.Success(
                    new UploadAudioPracticeResponse(Transcript: normalizedServer));
            }

            if (!string.IsNullOrWhiteSpace(normalizedClient) && !TranscriptHelper.IsServerConfirmedSilence(detailed, sttException))
            {
                return Result<UploadAudioPracticeResponse>.Success(
                    new UploadAudioPracticeResponse(Transcript: normalizedClient));
            }

            if (sttException != null)
            {
                return Result<UploadAudioPracticeResponse>.Failure($"Lỗi khi bóc băng âm thanh: {sttException.Message}");
            }

            return Result<UploadAudioPracticeResponse>.Failure("Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền). Vui lòng nói gần microphone hơn và thử lại.");
        }
        catch (System.Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(request.ClientTranscript))
            {
                var normalized = _speechToTextService.NormalizeTranscript(request.ClientTranscript);
                var finalTranscript = TranscriptHelper.NormalizeOrFallback(request.ClientTranscript, normalized);

                if (!string.IsNullOrWhiteSpace(finalTranscript))
                {
                    return Result<UploadAudioPracticeResponse>.Success(
                        new UploadAudioPracticeResponse(Transcript: finalTranscript));
                }
            }

            return Result<UploadAudioPracticeResponse>.Failure($"Lỗi khi bóc băng âm thanh: {ex.Message}");
        }
        finally
        {
            bufferStream?.Dispose();
        }
    }
}
