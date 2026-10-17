using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ tổng hợp giọng nói AI giám khảo (Text-to-Speech) chuẩn hóa cho khảo thí vấn đáp Kỹ thuật Phần mềm.
/// Hỗ trợ giọng Nữ (vi-VN-HoaiMyNeural) và giọng Nam (vi-VN-NamMinhNeural), bộ nhớ đệm SHA-256 và cơ chế tự động chuyển đổi dự phòng.
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Tổng hợp văn bản câu hỏi thành dữ liệu âm thanh MP3/WAV.
    /// </summary>
    Task<SpeechSynthesisResult> SynthesizeSpeechAsync(
        string text,
        string? voiceName = null,
        double speakingRate = 1.0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách các giọng đọc AI giám khảo khả dụng.
    /// </summary>
    IReadOnlyList<TtsVoiceOptionDto> GetAvailableVoices();
}
