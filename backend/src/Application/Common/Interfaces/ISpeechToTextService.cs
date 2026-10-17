using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Common.Interfaces;

public interface ISpeechToTextService
{
    /// <summary>
    /// Bóc băng âm thanh thành văn bản sử dụng Cloudflare Whisper AI (Phương án 1) kèm tự động dự phòng sang Gemini Multimodal STT (Phương án 2).
    /// </summary>
    Task<string> TranscribeAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bóc băng âm thanh thành kết quả chi tiết (kèm Provider, trạng thái Fallback, TimestampsJson).
    /// Mặc định ủy thác về TranscribeAudioAsync để tương thích ngược 100% với các mock hiện hữu.
    /// </summary>
    async Task<SpeechTranscriptionResult> TranscribeDetailedAsync(
        Stream audioStream,
        string fileName,
        string? vocabularyHint = null,
        CancellationToken cancellationToken = default)
    {
        var text = await TranscribeAudioAsync(audioStream, fileName, cancellationToken);
        return new SpeechTranscriptionResult(
            Text: text,
            RawText: text,
            Provider: "DefaultSpeechToText",
            UsedFallback: false,
            TimestampsJson: null,
            ConfidenceScore: null);
    }

    /// <summary>
    /// Chuẩn hóa văn bản bóc băng sang thuật ngữ Kỹ thuật Phần mềm (Code-Switching).
    /// </summary>
    string NormalizeTranscript(string? rawTranscript) => (rawTranscript ?? string.Empty).Trim();
}

