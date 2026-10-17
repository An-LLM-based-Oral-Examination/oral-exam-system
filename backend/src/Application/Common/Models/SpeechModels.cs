using System;
using System.Collections.Generic;

namespace OralExamination.Application.Common.Models;

/// <summary>
/// Kết quả bóc băng giọng nói (Speech-to-Text) chi tiết, bao gồm thông tin nhà cung cấp (Option 1 hoặc Option 2 Fallback),
/// văn bản đã chuẩn hóa thuật ngữ SE (Code-Switching) và chuỗi JSON mốc thời gian từ ngữ (nếu có).
/// </summary>
public sealed record SpeechTranscriptionResult(
    string Text,
    string RawText,
    string Provider,
    bool UsedFallback,
    string? TimestampsJson = null,
    double? ConfidenceScore = null
);

/// <summary>
/// Kết quả tổng hợp giọng đọc AI (Text-to-Speech) từ Backend.
/// Hỗ trợ Option 1 (Edge Neural TTS) và tự động chuyển đổi sang Option 2 (Gemini / Google Neural TTS) khi gặp lỗi.
/// </summary>
public sealed record SpeechSynthesisResult(
    byte[] AudioBytes,
    string ContentType,
    string Provider,
    string VoiceName,
    double SpeakingRate,
    bool UsedFallback,
    bool FromCache,
    string CacheKeySha256
);

/// <summary>
/// Cấu hình giọng đọc AI giám khảo hỗ trợ trên hệ thống.
/// </summary>
public sealed record TtsVoiceOptionDto(
    string VoiceId,
    string DisplayName,
    string Gender,
    string Locale,
    bool IsDefault
);
