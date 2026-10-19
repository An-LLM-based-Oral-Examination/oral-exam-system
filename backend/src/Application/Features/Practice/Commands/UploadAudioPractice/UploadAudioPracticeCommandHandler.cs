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
            System.Exception? sttException = null;
            string? transcript = null;

            try
            {
                SpeechTranscriptionResult? detailed = null;
                // Luôn gọi TranscribeDetailedAsync ngay cả khi VocabularyHint là null/rỗng
                // để kích hoạt Silero VAD, default SE prompt và bộ lọc ảo giác tiếng ồn.
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
                ? (_speechToTextService.NormalizeTranscript(transcript) ?? transcript.Trim())
                : string.Empty;
            var normalizedClient = !string.IsNullOrWhiteSpace(request.ClientTranscript)
                ? (_speechToTextService.NormalizeTranscript(request.ClientTranscript) ?? request.ClientTranscript.Trim())
                : string.Empty;

            // Luôn ưu tiên kết quả từ AI Server STT (Gemini 3.5 Transcribe / Whisper) nếu khác rỗng.
            // Chỉ dùng Browser WebSpeech ClientTranscript khi Server STT rỗng hoặc lỗi.
            if (!string.IsNullOrWhiteSpace(normalizedServer))
            {
                return Result<UploadAudioPracticeResponse>.Success(
                    new UploadAudioPracticeResponse(Transcript: normalizedServer));
            }

            if (!string.IsNullOrWhiteSpace(normalizedClient))
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
                var finalTranscript = !string.IsNullOrWhiteSpace(normalized)
                    ? normalized
                    : request.ClientTranscript.Trim();

                return Result<UploadAudioPracticeResponse>.Success(
                    new UploadAudioPracticeResponse(Transcript: finalTranscript));
            }

            return Result<UploadAudioPracticeResponse>.Failure($"Lỗi khi bóc băng âm thanh: {ex.Message}");
        }
        finally
        {
            bufferStream?.Dispose();
        }
    }
}
