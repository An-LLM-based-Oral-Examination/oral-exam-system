using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.MockExams.Commands.SubmitMockExamAnswer;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;
using OralExamination.Application.Features.Speech.Commands.TranscribeSpeech;
using OralExamination.Application.Features.Speech.Queries.GetTtsVoices;
using OralExamination.Application.Features.Speech.Queries.SynthesizeSpeech;
using OralExamination.Domain.Entities;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OralExamination.UnitTests.Services;

public class ResilientSpeechServicesTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ResiliencePipeline CreateFastNoRetryPipeline()
    {
        return new ResiliencePipelineBuilder().Build();
    }

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OralExamDbContext(options);
    }

    [Fact(DisplayName = "1. SeTerminologyNormalizer.Normalize chuẩn hóa chính xác các thuật ngữ SE phiên âm sai sang tiếng Anh chuẩn")]
    public void Normalize_WithMispronouncedSeTerms_ReturnsStandardizedSeTerminology()
    {
        var raw = "Em sử dụng cờ lin a ki téc chơ kết hợp si quy a ét, mê đi a tờ và bao đựt chan nồ trên nền tảng đốt nét si sáp.";

        var normalized = SeTerminologyNormalizer.Normalize(raw);

        normalized.Should().Be("Em sử dụng Clean Architecture kết hợp CQRS, MediatR và BoundedChannel trên nền tảng .NET C#.");
    }

    [Fact(DisplayName = "2. SeTerminologyNormalizer.PrepareTextForTts làm sạch Markdown và chuyển ký hiệu toán học/C#/.NET tự nhiên")]
    public void PrepareTextForTts_WithMarkdownAndTechnicalSymbols_ReturnsCleanNaturalText()
    {
        var raw = "### Câu hỏi **1**: Trong `.NET` 8 và `C#`, khi điểm số >= 4.0 và <= 8.0 trên thang 10.0đ thì hệ thống xử lý thế nào?";

        var prepared = SeTerminologyNormalizer.PrepareTextForTts(raw);

        prepared.Should().NotContain("**");
        prepared.Should().NotContain("`");
        prepared.Should().NotContain("###");
        prepared.Should().Contain("dot NET");
        prepared.Should().Contain("C sharp");
        prepared.Should().Contain("lớn hơn hoặc bằng");
        prepared.Should().Contain("nhỏ hơn hoặc bằng");
        prepared.Should().Contain("10 điểm");
    }

    [Fact(DisplayName = "3. ResilientSpeechToTextService: Phương án 1 (CloudflareWhisper) thành công trả về Provider CloudflareWhisper và UsedFallback = false")]
    public async Task TranscribeDetailedAsync_WhenPrimaryWhisperSucceeds_ReturnsCloudflareResultWithoutFallback()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456",
            ["Gemini:ApiKey"] = "gemini-key-789"
        });

        var geminiCalled = false;
        var whisperHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = "{\"result\":{\"text\":\"Triển khai đì pen đần xi in vơ sần trong cờ lin a ki téc chơ\",\"words\":[{\"word\":\"Clean\",\"start\":0.1,\"end\":0.5}]}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var geminiHandler = new MockHttpMessageHandler((req, ct) =>
        {
            geminiCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var primary = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());
        var fallback = new GeminiSpeechToTextService(new HttpClient(geminiHandler), config);
        var resilientStt = new ResilientSpeechToTextService(primary, fallback);

        using var audioStream = new MemoryStream(Encoding.UTF8.GetBytes("valid-webm-audio-payload"));

        var result = await resilientStt.TranscribeDetailedAsync(audioStream, "answer.webm");

        result.Provider.Should().Be("CloudflareWhisper");
        result.UsedFallback.Should().BeFalse();
        result.Text.Should().Be("Triển khai Dependency Inversion trong Clean Architecture");
        result.TimestampsJson.Should().NotBeNullOrWhiteSpace();
        geminiCalled.Should().BeFalse("Khi Phương án 1 thành công thì không được gọi Phương án 2");
    }

    [Fact(DisplayName = "4. ResilientSpeechToTextService: Phương án 1 ném lỗi HTTP 500 tự động rewind stream và chuyển sang Phương án 2 (GeminiMultimodalStt)")]
    public async Task TranscribeDetailedAsync_WhenPrimaryThrowsException_AutomaticallyFallsBackToGeminiStt()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456",
            ["Gemini:ApiKey"] = "gemini-key-789"
        });

        var originalBytes = Encoding.UTF8.GetBytes("student-voice-binary-stream-data");
        string? geminiRequestBody = null;

        var whisperHandler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Cloudflare Worker AI 500 Error", Encoding.UTF8, "text/plain")
            });
        });

        var geminiHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            geminiRequestBody = await req.Content!.ReadAsStringAsync(ct);
            var geminiResponse = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Sử dụng bao đựt chan nồ và pô li retry để chấm ngầm\"}]}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiResponse, Encoding.UTF8, "application/json")
            };
        });

        var primary = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());
        var fallback = new GeminiSpeechToTextService(new HttpClient(geminiHandler), config);
        var resilientStt = new ResilientSpeechToTextService(primary, fallback);

        using var audioStream = new MemoryStream(originalBytes);

        var result = await resilientStt.TranscribeDetailedAsync(audioStream, "student.webm");

        result.Provider.Should().Be("GeminiMultimodalStt");
        result.UsedFallback.Should().BeTrue();
        result.Text.Should().Be("Sử dụng BoundedChannel và Polly Retry để chấm ngầm");
        geminiRequestBody.Should().NotBeNull();
        geminiRequestBody.Should().Contain(Convert.ToBase64String(originalBytes), "Stream phải được rewind đầy đủ không mất byte nào khi chuyển sang Phương án 2");
    }

    [Fact(DisplayName = "5. ResilientSpeechToTextService: Phương án 1 trả về chuỗi rỗng trên audio có dữ liệu tự động chuyển sang Phương án 2")]
    public async Task TranscribeDetailedAsync_WhenPrimaryReturnsEmptyText_AutomaticallyFallsBackToGeminiStt()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456",
            ["Gemini:ApiKey"] = "gemini-key-789"
        });

        var whisperHandler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":{\"text\":\"   \"}}", Encoding.UTF8, "application/json")
            });
        });

        var geminiHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var validJsonResponse = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"text\\\":\\\"Áp dụng flu ần va li đây sần cho barem 10 điểm\\\"}\"}]}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validJsonResponse, Encoding.UTF8, "application/json")
            });
        });

        var primary = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());
        var fallback = new GeminiSpeechToTextService(new HttpClient(geminiHandler), config);
        var resilientStt = new ResilientSpeechToTextService(primary, fallback);

        using var audioStream = new MemoryStream(Encoding.UTF8.GetBytes("non-empty-audio"));

        var result = await resilientStt.TranscribeDetailedAsync(audioStream, "answer.wav");

        result.Provider.Should().Be("GeminiMultimodalStt");
        result.UsedFallback.Should().BeTrue();
        result.Text.Should().Be("Áp dụng FluentValidation cho barem 10 điểm");
    }

    [Fact(DisplayName = "6. ResilientTextToSpeechService: Khi Phương án 1 (EdgeNeuralTts) thành công trả về UsedFallback = false và đúng giọng chọn")]
    public async Task SynthesizeSpeechAsync_WhenPrimaryEdgeSucceeds_ReturnsEdgeResultWithoutFallback()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var expectedMp3 = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x01, 0x02 };
        var httpCalled = false;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            httpCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) => Task.FromResult(expectedMp3));

        var result = await ttsService.SynthesizeSpeechAsync(
            "Giải thích nguyên lý Dependency Inversion trong .NET",
            "vi-VN-NamMinhNeural",
            1.05);

        result.Provider.Should().Be("EdgeNeuralTts");
        result.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        result.UsedFallback.Should().BeFalse();
        result.FromCache.Should().BeFalse();
        result.AudioBytes.Should().BeEquivalentTo(expectedMp3);
        httpCalled.Should().BeFalse("Khi Phương án 1 thành công thì không gọi HTTP Fallback");
    }

    [Fact(DisplayName = "7. ResilientTextToSpeechService: Khi Phương án 1 lỗi tự động chuyển sang Phương án 2 (GoogleNeuralTranslateTts) với UsedFallback = true")]
    public async Task SynthesizeSpeechAsync_WhenPrimaryEdgeFails_AutomaticallyFallsBackToGoogleNeuralTts()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var fallbackMp3Bytes = new byte[] { 0xFF, 0xFB, 0x90, 0x64, 0x11, 0x22, 0x33 };
        var googleCallCount = 0;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            googleCallCount++;
            req.RequestUri!.Host.Should().Be("translate.googleapis.com");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(fallbackMp3Bytes)
            });
        });

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
                throw new HttpRequestException("Edge WebSocket 403 Forbidden"));

        var result = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi kiểm thử cơ chế tự động chuyển đổi dự phòng TTS",
            "vi-VN-HoaiMyNeural",
            1.0);

        result.Provider.Should().Be("GoogleNeuralTranslateTts");
        result.UsedFallback.Should().BeTrue();
        result.FromCache.Should().BeFalse();
        result.AudioBytes.Should().BeEquivalentTo(fallbackMp3Bytes);
        googleCallCount.Should().Be(1);
    }

    [Fact(DisplayName = "8. ResilientTextToSpeechService: Lưu cache SHA-256 giúp lần gọi thứ 2 trả về FromCache = true ngay lập tức không tạo thêm request")]
    public async Task SynthesizeSpeechAsync_OnSecondCallWithSameParameters_ReturnsFromSha256Cache()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var mp3Bytes = new byte[] { 0x49, 0x44, 0x33, 0x99, 0x88, 0x77 };
        var synthCallCount = 0;

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                synthCallCount++;
                return Task.FromResult(mp3Bytes);
            });

        var firstResult = await ttsService.SynthesizeSpeechAsync(
            "Hãy phân biệt CQRS và MediatR trong Clean Architecture.",
            "vi-VN-HoaiMyNeural",
            1.0);

        var secondResult = await ttsService.SynthesizeSpeechAsync(
            "Hãy phân biệt CQRS và MediatR trong Clean Architecture.",
            "vi-VN-HoaiMyNeural",
            1.0);

        firstResult.FromCache.Should().BeFalse();
        secondResult.FromCache.Should().BeTrue();
        secondResult.CacheKeySha256.Should().Be(firstResult.CacheKeySha256);
        secondResult.AudioBytes.Should().BeEquivalentTo(mp3Bytes);
        synthCallCount.Should().Be(1, "Lần gọi thứ 2 phải lấy từ bộ nhớ đệm SHA-256 mà không gọi lại synthesizer");
    }

    [Fact(DisplayName = "9. SynthesizeSpeechQueryHandler và GetTtsVoicesQueryHandler xử lý đúng chuẩn CQRS")]
    public async Task SpeechQueryHandlers_ReturnExpectedVoicesAndAudioSynthesis()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var sampleMp3 = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) => Task.FromResult(sampleMp3));

        var voicesHandler = new GetTtsVoicesQueryHandler(ttsService);
        var voicesResult = await voicesHandler.Handle(new GetTtsVoicesQuery(), CancellationToken.None);

        voicesResult.IsSuccess.Should().BeTrue();
        voicesResult.Value.Should().NotBeNull();
        voicesResult.Value.Should().HaveCount(2);
        voicesResult.Value.Should().Contain(v => v.VoiceId == "vi-VN-HoaiMyNeural" && v.IsDefault);
        voicesResult.Value.Should().Contain(v => v.VoiceId == "vi-VN-NamMinhNeural");

        var synthHandler = new SynthesizeSpeechQueryHandler(ttsService);
        var synthResult = await synthHandler.Handle(
            new SynthesizeSpeechQuery("Câu hỏi vấn đáp số 1", "vi-VN-NamMinhNeural", 1.1),
            CancellationToken.None);

        synthResult.IsSuccess.Should().BeTrue();
        synthResult.Value.Should().NotBeNull();
        synthResult.Value!.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        synthResult.Value.AudioBytes.Should().BeEquivalentTo(sampleMp3);
    }

    [Fact(DisplayName = "10. TranscribeSpeechCommandHandler trả về kết quả bóc băng đã chuẩn hóa thuật ngữ SE")]
    public async Task TranscribeSpeechCommandHandler_WithValidAudioStream_ReturnsNormalizedResult()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456"
        });

        var whisperHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = "{\"result\":{\"text\":\"Em dùng đi bi con tếch với ác nô trắc kinh trong en ti ti phờ rêm guốc\"}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var primary = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());
        var fallback = new GeminiSpeechToTextService(new HttpClient(), config);
        var resilientStt = new ResilientSpeechToTextService(primary, fallback);
        var handler = new TranscribeSpeechCommandHandler(resilientStt);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("audio-content"));
        var commandResult = await handler.Handle(
            new TranscribeSpeechCommand(stream, "answer.webm"),
            CancellationToken.None);

        commandResult.IsSuccess.Should().BeTrue();
        commandResult.Value.Should().NotBeNull();
        commandResult.Value!.Text.Should().Be("Em dùng DbContext với AsNoTracking trong Entity Framework Core");
        commandResult.Value.Provider.Should().Be("CloudflareWhisper");
        commandResult.Value.UsedFallback.Should().BeFalse();
    }

    [Fact(DisplayName = "11. SeTerminologyNormalizer.Normalize chuẩn hóa các thuật ngữ PRN231/SWD392/SWR302 nâng cao")]
    public void Normalize_WithAdvancedSeCourseTerminology_ReturnsStandardizedSeTerms()
    {
        var raw = "Triển khai rê pô xi tô ri kết hợp iu nít óp guốc, ô oắt hai với pi cây si i và sa ga pát tần theo cáp thê ô rầm.";

        var normalized = SeTerminologyNormalizer.Normalize(raw);

        normalized.Should().Be("Triển khai Repository Pattern kết hợp Unit of Work, OAuth 2.0 với PKCE và Saga Pattern theo CAP Theorem.");
    }

    [Fact(DisplayName = "12. TranscribeSpeechCommandHandler: Khi thiếu API Key trên Cloud nhưng có ClientTranscript thì tự động chuẩn hóa SE và trả về BrowserWebSpeechHybrid")]
    public async Task TranscribeSpeechCommandHandler_WhenCloudKeysMissingAndClientTranscriptProvided_ReturnsNormalizedHybridResult()
    {
        var emptyConfig = BuildConfig(new Dictionary<string, string?>());
        var primary = new CloudflareWhisperService(new HttpClient(), emptyConfig, CreateFastNoRetryPipeline());
        var fallback = new GeminiSpeechToTextService(new HttpClient(), emptyConfig);
        var resilientStt = new ResilientSpeechToTextService(primary, fallback);
        var handler = new TranscribeSpeechCommandHandler(resilientStt);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("webm-audio-without-cloud-keys"));
        var commandResult = await handler.Handle(
            new TranscribeSpeechCommand(
                stream,
                "answer.webm",
                VocabularyHint: null,
                ClientTranscript: "Em áp dụng cờ lin a ki téc chơ và rê pô xi tô ri trong đốt nét"),
            CancellationToken.None);

        commandResult.IsSuccess.Should().BeTrue();
        commandResult.Value.Should().NotBeNull();
        commandResult.Value!.Text.Should().Be("Em áp dụng Clean Architecture và Repository Pattern trong .NET");
        commandResult.Value.Provider.Should().Be("BrowserWebSpeechHybrid");
        commandResult.Value.UsedFallback.Should().BeTrue();
    }

    [Fact(DisplayName = "13. Chặn hoàn toàn ảo giác tiếng ồn Whisper (Thank you very much lặp lại) và tự động fallback")]
    public async Task TranscribeDetailedAsync_WhenWhisperReturnsNoiseHallucinationLoop_FiltersHallucinationAndFallsBack()
    {
        var hallucinationLoop = "Thank you very much. Thank you very much. Thank you very much. Thank you very much. Thank you very much.";
        SeTerminologyNormalizer.IsHallucinationOrNoise(hallucinationLoop).Should().BeTrue();
        SeTerminologyNormalizer.Normalize(hallucinationLoop).Should().BeEmpty();

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456",
            ["Gemini:ApiKey"] = "gemini-key-789"
        });

        var whisperHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = $"{{\"result\":{{\"text\":\"{hallucinationLoop}\"}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var geminiHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Em áp dụng cờ lin a ki téc chơ và si quy a ét\"}]}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var primary = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());
        var fallback = new GeminiSpeechToTextService(new HttpClient(geminiHandler), config);
        var resilientStt = new ResilientSpeechToTextService(primary, fallback);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("noisy-mic-audio"));
        var result = await resilientStt.TranscribeDetailedAsync(stream, "answer.webm", null, CancellationToken.None);

        result.Text.Should().Be("Em áp dụng Clean Architecture và CQRS");
        result.UsedFallback.Should().BeTrue();
    }

    [Fact(DisplayName = "14. SeTerminologyNormalizer.NormalizeWithContext và BuildContextualWhisperPrompt dự đoán chuẩn thuật ngữ theo ngữ cảnh câu hỏi")]
    public void NormalizeWithContext_WithQuestionVocabularyHint_PredictsAndAlignsTechnicalTerms()
    {
        var questionContext = "PRN231. Hãy phân tích cơ chế hoạt động của OneWayLockInterceptor và GradingQueueWorker trong hệ thống.";
        var prompt = SeTerminologyNormalizer.BuildContextualWhisperPrompt(questionContext);

        prompt.Should().Contain(SeTerminologyNormalizer.DefaultSeWhisperPrompt);
        prompt.Should().EndWith($"Câu hỏi hiện tại: {questionContext}");

        var rawTranscript = "Em dùng onewaylockinterceptor kết hợp gradingqueueworker và xô lít với mít đồ que trong pốt gờ re.";
        var normalized = SeTerminologyNormalizer.NormalizeWithContext(rawTranscript, questionContext);

        normalized.Should().Be("Em dùng OneWayLockInterceptor kết hợp GradingQueueWorker và SOLID với Middleware trong PostgreSQL.");
    }

    [Fact(DisplayName = "15. ResilientTextToSpeechService: Khi chọn giọng Nam (vi-VN-NamMinhNeural) trên Windows ưu tiên tổng hợp giọng Nam thật Microsoft An (audio/wav)")]
    public async Task SynthesizeSpeechAsync_WhenMaleVoiceRequestedOnWindows_SynthesizesRealMaleVoiceOrValidAudio()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Speech:DisableEdgePrimary"] = "true"
        });

        var fallbackMp3Bytes = new byte[] { 0xFF, 0xFB, 0x90, 0x64 };
        var httpHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(fallbackMp3Bytes)
            }));

        var ttsService = new ResilientTextToSpeechService(new HttpClient(httpHandler), config);
        var result = await ttsService.SynthesizeSpeechAsync(
            "Xin chào sinh viên, hãy giải thích kiến trúc Clean Architecture.",
            "vi-VN-NamMinhNeural",
            1.0);

        result.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        result.AudioBytes.Should().NotBeEmpty();

        if (OperatingSystem.IsWindows() && result.Provider == "WindowsOneCoreMaleTts")
        {
            result.ContentType.Should().Be("audio/wav");
            result.UsedFallback.Should().BeFalse();
        }
    }

    [Fact(DisplayName = "16. Phương án 1 (STT-A): Khi Speech:PrimarySttProvider = Gemini thì ưu tiên gemini-3.5-transcribe (audioTranscriptionConfig + customVocabulary) và tự động fallback sang CloudflareWhisper khi lỗi")]
    public async Task TranscribeDetailedAsync_WhenConfiguredForGeminiPrimary_UsesGemini35TranscribeFirstAndFallsBackToWhisper()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Speech:PrimarySttProvider"] = "Gemini",
            ["Gemini:ApiKey"] = "AQ.Ab8RN6L_PrimaryKey",
            ["Cloudflare:AccountId"] = "cf-acc-fallback",
            ["Cloudflare:ApiToken"] = "cf-token-fallback"
        });

        string? capturedGeminiUrl = null;
        string? capturedGeminiHeader = null;
        string? capturedGeminiPayload = null;
        var whisperCalled = false;

        var geminiSuccessHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedGeminiUrl = req.RequestUri!.ToString();
            capturedGeminiHeader = req.Headers.TryGetValues("x-goog-api-key", out var values) ? string.Join(",", values) : null;
            capturedGeminiPayload = await req.Content!.ReadAsStringAsync(ct);

            var responseJson = "{\"candidates\":[{\"content\":{\"parts\":[{\"audioTranscription\":{\"text\":\"Em triển khai OneWayLockInterceptor và bao đựt chan nồ trong đốt nét 8\"}}]}}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var whisperUnusedHandler = new MockHttpMessageHandler((req, ct) =>
        {
            whisperCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var primaryWhisper = new CloudflareWhisperService(new HttpClient(whisperUnusedHandler), config, CreateFastNoRetryPipeline());
        var geminiStt = new GeminiSpeechToTextService(new HttpClient(geminiSuccessHandler), config);
        var resilientStt = new ResilientSpeechToTextService(primaryWhisper, geminiStt, logger: null, configuration: config);

        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("student-audio-1"));
        var primaryResult = await resilientStt.TranscribeDetailedAsync(
            stream1,
            "01_SE170123.webm",
            "Hãy trình bày cơ chế hoạt động của OneWayLockInterceptor.",
            CancellationToken.None);

        primaryResult.Provider.Should().Be("GeminiMultimodalStt");
        primaryResult.UsedFallback.Should().BeFalse();
        primaryResult.Text.Should().Be("Em triển khai OneWayLockInterceptor và BoundedChannel trong .NET 8");
        whisperCalled.Should().BeFalse();
        capturedGeminiUrl.Should().Contain("gemini-3.5-transcribe:generateContent");
        capturedGeminiUrl.Should().NotContain("key=", "API Key bắt buộc chỉ gửi qua header x-goog-api-key");
        capturedGeminiHeader.Should().Be("AQ.Ab8RN6L_PrimaryKey");
        capturedGeminiPayload.Should().Contain("audioTranscriptionConfig");
        capturedGeminiPayload.Should().Contain("customVocabulary");
        capturedGeminiPayload.Should().Contain("OneWayLockInterceptor");
        capturedGeminiPayload.Should().Contain("\"SMART\"");

        // Kiểm tra nhánh tự động Fallback sang CloudflareWhisper khi Gemini 3.5 Transcribe trả lỗi 503
        var geminiFailHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Gemini 503 Overloaded", Encoding.UTF8, "text/plain")
            }));

        var whisperFallbackHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = "{\"result\":{\"text\":\"Em dùng pô li retry và đét lét tơ kiu\"}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var resilientSttWithFallback = new ResilientSpeechToTextService(
            new CloudflareWhisperService(new HttpClient(whisperFallbackHandler), config, CreateFastNoRetryPipeline()),
            new GeminiSpeechToTextService(new HttpClient(geminiFailHandler), config),
            logger: null,
            configuration: config);

        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("student-audio-2"));
        var fallbackResult = await resilientSttWithFallback.TranscribeDetailedAsync(stream2, "01_SE170123.webm");

        fallbackResult.Provider.Should().Be("CloudflareWhisper");
        fallbackResult.UsedFallback.Should().BeTrue();
        fallbackResult.Text.Should().Be("Em dùng Polly Retry và Dead-Letter Queue");
    }

    [Fact(DisplayName = "17. Gemini 3.8 TTS: Gửi verbatim transcript + speech_metadata.style (không chèn lệnh đạo diễn vào text) và đóng gói chuẩn audio/wav")]
    public async Task SynthesizeSpeechAsync_WhenFallingBackToGemini38Tts_SendsVerbatimTextAndReturnsWavAudio()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Speech:DisableEdgePrimary"] = "true",
            ["Speech:PreferGeminiTtsFallback"] = "true",
            ["Gemini:ApiKey"] = "gemini-tts-key-999"
        });

        string? capturedPayload = null;
        string? capturedUrl = null;
        var rawPcmSamples = new byte[] { 0x10, 0x00, 0x20, 0x00, 0x30, 0x00, 0x40, 0x00 };
        var base64Pcm = Convert.ToBase64String(rawPcmSamples);

        var httpHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedUrl = req.RequestUri!.ToString();
            capturedPayload = await req.Content!.ReadAsStringAsync(ct);
            var responseJson = $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"inlineData\":{{\"mimeType\":\"audio/L16;rate=24000\",\"data\":\"{base64Pcm}\"}}}}]}}}}]}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) => Task.FromResult(Array.Empty<byte>()));

        var result = await ttsService.SynthesizeSpeechAsync(
            "Hãy trình bày nguyên lý SOLID.",
            "vi-VN-HoaiMyNeural",
            1.0);

        result.Provider.Should().Be("GeminiAudioTts");
        result.ContentType.Should().Be("audio/wav");
        result.AudioBytes.Length.Should().Be(44 + rawPcmSamples.Length, "PCM16 từ Gemini 3.8 TTS phải được bọc header RIFF WAV 44 bytes");
        Encoding.ASCII.GetString(result.AudioBytes, 0, 4).Should().Be("RIFF");
        Encoding.ASCII.GetString(result.AudioBytes, 8, 4).Should().Be("WAVE");

        capturedUrl.Should().Contain("gemini-3.8-flash-lite-tts:generateContent");
        capturedUrl.Should().NotContain("key=");
        capturedPayload.Should().NotBeNull();
        capturedPayload.Should().Contain("speech_metadata");
        capturedPayload.Should().NotContain("Hãy đọc rõ ràng câu hỏi", "Gemini 3.8 TTS đọc nguyên văn parts[].text nên cấm nhét lời nhắc vào text");
    }

    [Fact(DisplayName = "18. GeminiSpeechToTextService: Không bị biến Gemini__ApiUrl (gemini-3.6-flash) ghi đè mất ưu tiên của gemini-3.5-transcribe")]
    public async Task GeminiSpeechToTextService_WhenGeminiApiUrlIsTextModel_StillPrioritizesGemini35Transcribe()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "gemini-env-key-123",
            ["Gemini__ApiUrl"] = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent"
        });

        string? firstCalledUrl = null;
        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            firstCalledUrl ??= req.RequestUri!.ToString();
            var json = @"{""candidates"":[{""content"":{""parts"":[{""text"":""Em sử dụng Clean Architecture và MediatR""}]}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var service = new GeminiSpeechToTextService(new HttpClient(httpHandler), config);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("webm-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "01_SE170123.webm");

        firstCalledUrl.Should().NotBeNull();
        firstCalledUrl.Should().Contain("gemini-3.5-transcribe:generateContent");
        result.Text.Should().Contain("Clean Architecture");
    }

    [Fact(DisplayName = "19. GeminiAiGradingService: Chấm điểm dự phòng định lượng theo Barem Rubric khi API lỗi giúp kích hoạt Follow-up MF-01 và Scorecard MF-02")]
    public async Task GeminiAiGradingService_WhenHttpFails_EvaluatesByRubricFallbackForFollowUpAndScorecard()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "invalid-local-key"
        });

        var failHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(@"{""error"":{""message"":""API key not valid""}}", Encoding.UTF8, "application/json")
            }));

        var gradingService = new GeminiAiGradingService(new HttpClient(failHandler), config);
        var crit1Id = Guid.NewGuid().ToString();
        var crit2Id = Guid.NewGuid().ToString();

        var request = new OralExamination.Application.Common.Interfaces.AiGradingRequest
        {
            QuestionContent = "Trình bày kiến trúc Clean Architecture và vai trò của tầng Domain, Application.",
            SampleAnswer = "Clean Architecture gồm 4 tầng Domain, Application, Infrastructure, API. Tầng Domain chứa Entities và quy tắc nghiệp vụ cốt lõi, độc lập hoàn toàn với Database và Framework.",
            Transcript = "Theo em Clean Architecture chia hệ thống thành các tầng Domain và Application giúp tách biệt nghiệp vụ cốt lõi khỏi cơ sở dữ liệu.",
            Criteria = new List<OralExamination.Application.Common.Interfaces.AiGradingCriterion>
            {
                new() { Id = crit1Id, Name = "Cấu trúc 4 tầng", Description = "Nêu đúng Domain, Application, Infrastructure, API", MaxScore = 5.0m, Weight = 0.5m },
                new() { Id = crit2Id, Name = "Nguyên tắc phụ thuộc", Description = "Độc lập hoàn toàn với Database và Framework", MaxScore = 5.0m, Weight = 0.5m }
            }
        };

        Func<Task> act = () => gradingService.GradeAnswerAsync(request);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory(DisplayName = "19b. GeminiAiGradingService: Chấm 0.0 điểm tuyệt đối khi sinh viên trả lời không biết, nhờ làm hộ hoặc lạc đề")]
    [InlineData("Tui không biết nữa bạn ơi! Bạn làm giùm tui ấy! ok fen")]
    [InlineData("Maybe.. I don't know..")]
    [InlineData("Tôi không biết nữa bạn ơi")]
    [InlineData("Em xin phép bỏ qua câu hỏi này thầy ơi")]
    public async Task GeminiAiGradingService_WhenTranscriptIsRefusalOrOffTopic_ReturnsZeroScore(string refusalTranscript)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "AQ.PlaceholderKey"
        });

        var failHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        var gradingService = new GeminiAiGradingService(new HttpClient(failHandler), config);
        var crit1Id = Guid.NewGuid().ToString();
        var crit2Id = Guid.NewGuid().ToString();

        var request = new OralExamination.Application.Common.Interfaces.AiGradingRequest
        {
            QuestionContent = "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.",
            SampleAnswer = "Command dùng để thay đổi trạng thái hệ thống (Insert/Update/Delete), còn Query dùng để truy vấn dữ liệu AsNoTracking mà không làm thay đổi trạng thái.",
            Transcript = refusalTranscript,
            Criteria = new List<OralExamination.Application.Common.Interfaces.AiGradingCriterion>
            {
                new() { Id = crit1Id, Name = "Phân biệt Command và Query", Description = "Nêu rõ vai trò ghi và đọc trong CQRS", MaxScore = 5.0m, Weight = 0.5m },
                new() { Id = crit2Id, Name = "Triển khai với MediatR", Description = "Sử dụng IRequest và IRequestHandler trong .NET 8", MaxScore = 5.0m, Weight = 0.5m }
            }
        };

        var result = await gradingService.GradeAnswerAsync(request);

        result.Score.Should().Be(0.0m, "Câu trả lời từ chối hoặc không có kiến thức chuyên môn bắt buộc phải nhận 0.0 điểm");
        result.IsSuspicious.Should().BeTrue();
        result.SuspiciousReason.Should().NotContain("Thiếu cấu hình Gemini API Key");
        result.Details.Should().HaveCount(2);
        result.Details.All(d => d.Score == 0.0m).Should().BeTrue();
    }

    [Fact(DisplayName = "19c. GeminiAiGradingService: Hỗ trợ khóa định dạng mới AQ.Ab8RN... và đồng bộ Score bằng đúng tổng điểm các tiêu chí Rubric đã clamp")]
    public async Task GeminiAiGradingService_WithNewAqKey_NormalizesCriteriaAndSyncsTotalScore()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "AQ.Ab8RN6L_MockUnitTestKey_123456"
        });

        var crit1Id = Guid.NewGuid().ToString();
        var crit2Id = Guid.NewGuid().ToString();
        string? sentHeaderKey = null;

        var geminiHandler = new MockHttpMessageHandler((req, ct) =>
        {
            if (req.Headers.TryGetValues("x-goog-api-key", out var values))
            {
                sentHeaderKey = values.FirstOrDefault();
            }

            // Giả lập LLM trả về Score tổng bị lệch (9.0) và điểm tiêu chí 1 vượt quá MaxScore (6.0 > 5.0), tiêu chí 2 khớp theo tên
            var llmJson = @"{
                ""candidates"": [{
                    ""content"": {
                        ""parts"": [{
                            ""text"": ""{\""Score\"": 9.0, \""ConfidenceScore\"": 0.92, \""IsSuspicious\"": false, \""Feedback\"": \""Giải thích tốt CQRS và MediatR.\"", \""CotTrace\"": \""CoT trace\"", \""Details\"": [{\""CriterionId\"": \""" + crit1Id + @"\"", \""Score\"": 6.0, \""Comment\"": \""Nêu đúng Command/Query\""}, {\""CriterionId\"": \""Triển khai với MediatR\"", \""Score\"": 3.5, \""Comment\"": \""Đúng IRequestHandler\""}]}""
                        }]
                    }
                }]
            }";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(llmJson, Encoding.UTF8, "application/json")
            });
        });

        var gradingService = new GeminiAiGradingService(new HttpClient(geminiHandler), config);
        var request = new OralExamination.Application.Common.Interfaces.AiGradingRequest
        {
            QuestionContent = "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.",
            SampleAnswer = "Command thay đổi dữ liệu, Query truy vấn dữ liệu qua IRequest và IRequestHandler.",
            Transcript = "Em xin trình bày: Command dùng để thay đổi trạng thái dữ liệu, còn Query dùng để đọc dữ liệu với IRequest và IRequestHandler trong MediatR.",
            Criteria = new List<OralExamination.Application.Common.Interfaces.AiGradingCriterion>
            {
                new() { Id = crit1Id, Name = "Phân biệt Command và Query", Description = "Vai trò đọc ghi", MaxScore = 5.0m, Weight = 0.5m },
                new() { Id = crit2Id, Name = "Triển khai với MediatR", Description = "IRequest và IRequestHandler", MaxScore = 5.0m, Weight = 0.5m }
            }
        };

        var result = await gradingService.GradeAnswerAsync(request);

        sentHeaderKey.Should().Be("AQ.Ab8RN6L_MockUnitTestKey_123456", "Khóa API của Google AI Studio phải được gửi trực tiếp qua header x-goog-api-key");
        result.Details.Should().HaveCount(2);
        result.Details[0].CriterionId.Should().Be(crit1Id);
        result.Details[0].Score.Should().Be(5.0m, "Điểm tiêu chí 1 (6.0) phải được clamp về đúng MaxScore (5.0)");
        result.Details[1].CriterionId.Should().Be(crit2Id, "Tiêu chí trả về theo tên phải được ánh xạ chuẩn xác về GUID CriterionId");
        result.Details[1].Score.Should().Be(3.5m);
        result.Score.Should().Be(8.5m, "Điểm tổng phải bằng đúng tổng điểm các tiêu chí sau khi chuẩn hóa (5.0 + 3.5 = 8.5)");
    }

    [Fact(DisplayName = "20. CloudflareWhisperService: Tính toán ConfidenceScore hiệu chuẩn chính xác từ avg_logprob và no_speech_prob của segments")]
    public async Task TranscribeDetailedAsync_WithSegmentMetrics_CalculatesCalibratedConfidenceScore()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456"
        });

        var responseJson = @"{
            ""result"": {
                ""text"": ""Giải thích nguyên lý SOLID trong C# .NET 8"",
                ""segments"": [
                    {
                        ""text"": ""Giải thích nguyên lý SOLID"",
                        ""avg_logprob"": -0.20,
                        ""no_speech_prob"": 0.05,
                        ""start"": 0.0,
                        ""end"": 2.0
                    },
                    {
                        ""text"": "" trong C# .NET 8"",
                        ""avg_logprob"": -0.30,
                        ""no_speech_prob"": 0.10,
                        ""start"": 2.0,
                        ""end"": 4.0
                    }
                ]
            }
        }";

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));

        var service = new CloudflareWhisperService(new HttpClient(handler), config, CreateFastNoRetryPipeline());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore!.Value.Should().BeInRange(0.70, 0.75, "ConfidenceScore trung bình từ 2 segments phải xấp xỉ 0.722");
        result.Text.Should().Be("Giải thích nguyên lý SOLID trong C# .NET 8");
    }

    [Fact(DisplayName = "21. CloudflareWhisperService: Loại bỏ 100% ảo giác âm thanh im lặng khi toàn bộ segments có no_speech_prob > 0.85")]
    public async Task TranscribeDetailedAsync_WhenAllSegmentsDominatedBySilence_DiscardsTextAndReturnsZeroConfidence()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456"
        });

        var silenceResponseJson = @"{
            ""result"": {
                ""text"": ""Thank you for watching. Please subscribe to my channel."",
                ""segments"": [
                    {
                        ""text"": ""Thank you for watching."",
                        ""avg_logprob"": -0.95,
                        ""no_speech_prob"": 0.96,
                        ""start"": 0.0,
                        ""end"": 1.5
                    },
                    {
                        ""text"": "" Please subscribe to my channel."",
                        ""avg_logprob"": -0.80,
                        ""no_speech_prob"": 0.91,
                        ""start"": 1.5,
                        ""end"": 3.0
                    }
                ]
            }
        }";

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(silenceResponseJson, Encoding.UTF8, "application/json")
            }));

        var service = new CloudflareWhisperService(new HttpClient(handler), config, CreateFastNoRetryPipeline());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("silence-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "silence.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().BeEmpty("Toàn bộ segments là âm thanh im lặng/nhiễu phải được loại bỏ triệt để");
        result.RawText.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(0.0);
    }

    [Fact(DisplayName = "22. CloudflareWhisperService: Lọc bỏ phân đoạn im lặng cục bộ nhưng giữ lại phân đoạn giọng nói hợp lệ")]
    public async Task TranscribeDetailedAsync_WithMixedSilenceAndSpeechSegments_FiltersSilenceAndPreservesValidSpeech()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456"
        });

        var mixedResponseJson = @"{
            ""result"": {
                ""text"": ""[tiếng thở] Em áp dụng cờ lin a ki téc chơ và si quy a ét"",
                ""segments"": [
                    {
                        ""text"": ""[tiếng thở]"",
                        ""avg_logprob"": -1.5,
                        ""no_speech_prob"": 0.94,
                        ""start"": 0.0,
                        ""end"": 1.0
                    },
                    {
                        ""text"": ""Em áp dụng cờ lin a ki téc chơ và si quy a ét"",
                        ""avg_logprob"": -0.15,
                        ""no_speech_prob"": 0.03,
                        ""start"": 1.0,
                        ""end"": 4.0
                    }
                ]
            }
        }";

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mixedResponseJson, Encoding.UTF8, "application/json")
            }));

        var service = new CloudflareWhisperService(new HttpClient(handler), config, CreateFastNoRetryPipeline());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("mixed-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "speech.webm", null, CancellationToken.None);

        result.Text.Should().Be("Em áp dụng Clean Architecture và CQRS");
        result.Text.Should().NotContain("tiếng thở");
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore!.Value.Should().BeInRange(0.80, 0.90);
    }

    [Fact(DisplayName = "23. CloudflareWhisperService: Xử lý an toàn khi segments rỗng hoặc thiếu thuộc tính avg_logprob không gây crash")]
    public async Task TranscribeDetailedAsync_WithEmptySegmentsOrMissingProperties_ParsesSafely()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456"
        });

        var emptySegmentsJson = @"{
            ""result"": {
                ""text"": ""Kiểm thử schema không có segments"",
                ""segments"": []
            }
        }";

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptySegmentsJson, Encoding.UTF8, "application/json")
            }));

        var service = new CloudflareWhisperService(new HttpClient(handler), config, CreateFastNoRetryPipeline());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Text.Should().Be("Kiểm thử schema không có segments");
        result.ConfidenceScore.Should().Be(0.90, "Mặc định an toàn cho Whisper Turbo khi có text nhưng thiếu segments array");
    }

    [Fact(DisplayName = "24. UploadAudioPracticeCommandHandler: Luôn gọi TranscribeDetailedAsync ngay cả khi VocabularyHint là null")]
    public async Task UploadAudioPracticeCommandHandler_WhenVocabularyHintNull_CallsTranscribeDetailedAsyncAndFallsBackToAudioAsync()
    {
        var sttMock = new Mock<ISpeechToTextService>();
        var expectedTranscript = "Phân tích nguyên lý Liskov Substitution Principle";

        // TranscribeDetailedAsync không setup -> Moq trả về null
        sttMock
            .Setup(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "sample.webm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscript);

        var handler = new UploadAudioPracticeCommandHandler(sttMock.Object);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("voice-stream"));
        var command = new UploadAudioPracticeCommand(stream, "sample.webm", "audio/webm", VocabularyHint: null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Transcript.Should().Be(expectedTranscript);

        // Xác minh TranscribeDetailedAsync được gọi đầu tiên với hint = null
        sttMock.Verify(s => s.TranscribeDetailedAsync(
            It.IsAny<Stream>(),
            "sample.webm",
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        // Xác minh TranscribeAudioAsync được gọi dự phòng vì mock detailed trả về null
        sttMock.Verify(s => s.TranscribeAudioAsync(
            It.IsAny<Stream>(),
            "sample.webm",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "25. UploadAudioPracticeCommandHandler: Khi TranscribeDetailedAsync xác định âm thanh im lặng thì không gọi lại TranscribeAudioAsync")]
    public async Task UploadAudioPracticeCommandHandler_WhenAudioIsSilence_DoesNotCallTranscribeAudioAsyncAgain()
    {
        var sttMock = new Mock<ISpeechToTextService>();
        sttMock
            .Setup(s => s.TranscribeDetailedAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult(
                Text: string.Empty,
                RawText: string.Empty,
                Provider: "CloudflareWhisper",
                UsedFallback: false,
                ConfidenceScore: 0.0));

        var handler = new UploadAudioPracticeCommandHandler(sttMock.Object);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("silence-bytes"));
        var command = new UploadAudioPracticeCommand(stream, "silence.webm", "audio/webm");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Không nhận diện được giọng nói rõ ràng");

        // Tuyệt đối không được gọi TranscribeAudioAsync lần 2
        sttMock.Verify(s => s.TranscribeAudioAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "26. ResilientTextToSpeechService: Google Translate TTS tự động truyền ttsspeed=0.5 khi speakingRate <= 0.85 (tốc độ chậm cho sinh viên)")]
    public async Task SynthesizeSpeechAsync_WhenGoogleTranslateFallbackAndSlowRate_PassesTtsSpeed05()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var sampleBytes = new byte[] { 0xFF, 0xFB, 0x90, 0x64 };
        string? capturedUrl = null;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sampleBytes)
            });
        });

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
                throw new HttpRequestException("Simulated Edge failure"));

        var result = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi thử nghiệm tốc độ chậm 0.8x cho sinh viên",
            "vi-VN-HoaiMyNeural",
            0.80);

        result.Provider.Should().Be("GoogleNeuralTranslateTts");
        result.SpeakingRate.Should().Be(0.80);
        result.UsedFallback.Should().BeTrue();
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain("ttsspeed=0.5");
        capturedUrl.Should().Contain("client=tw-ob");
        capturedUrl.Should().Contain("tl=vi");
    }

    [Theory(DisplayName = "27. ResilientTextToSpeechService: Google Translate TTS định dạng đúng ttsspeed cho các mức tốc độ bình thường và nhanh")]
    [InlineData(1.0, "ttsspeed=1.0")]
    [InlineData(1.25, "ttsspeed=1.25")]
    public async Task SynthesizeSpeechAsync_WhenGoogleTranslateFallbackAndNormalOrFastRate_PassesFormattedTtsSpeed(
        double rate,
        string expectedQueryPart)
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var sampleBytes = new byte[] { 0xFF, 0xFB, 0x90, 0x64 };
        string? capturedUrl = null;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sampleBytes)
            });
        });

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, r, ct) =>
                throw new HttpRequestException("Simulated Edge failure"));

        var result = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi thử nghiệm tốc độ bình thường hoặc nhanh",
            "vi-VN-HoaiMyNeural",
            rate);

        result.Provider.Should().Be("GoogleNeuralTranslateTts");
        result.SpeakingRate.Should().Be(rate);
        result.UsedFallback.Should().BeTrue();
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain(expectedQueryPart);
    }

    [Fact(DisplayName = "28. ResilientTextToSpeechService: Edge Neural kích hoạt cooldown khi lỗi và tự động phục hồi khi thành công hoặc qua ResetEdgeCooldown")]
    public async Task SynthesizeSpeechAsync_EdgeCooldown_ActivatesOnFailureBypassesEdgeAndRecoversOnSuccess()
    {
        ResilientTextToSpeechService.ResetEdgeCooldown();
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Speech:EdgeCooldownSeconds"] = "60"
        });
        var sampleMp3 = new byte[] { 0x49, 0x44, 0x33, 0x01, 0x02 };
        int edgeInvocationCount = 0;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sampleMp3)
            });
        });

        // 1. Tạo service với Edge override ném lỗi và enableCooldownForCustom: true
        var failingService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                Interlocked.Increment(ref edgeInvocationCount);
                throw new HttpRequestException("Edge connection dropped");
            },
            enableCooldownForCustom: true);

        var failResult = await failingService.SynthesizeSpeechAsync("Câu hỏi kích hoạt cooldown", "vi-VN-HoaiMyNeural", 1.0);

        failResult.UsedFallback.Should().BeTrue();
        edgeInvocationCount.Should().Be(1);
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeTrue("Edge phải bị đóng băng sau khi gặp lỗi");
        ResilientTextToSpeechService.EdgeCooldownUntilUtc.Should().NotBeNull();

        // 2. Lần gọi thứ 2 khi đang cooldown: Edge override không được gọi lại, đi thẳng vào fallback
        var secondResult = await failingService.SynthesizeSpeechAsync("Câu hỏi thứ hai trong lúc cooldown", "vi-VN-HoaiMyNeural", 1.0);
        secondResult.UsedFallback.Should().BeTrue();
        edgeInvocationCount.Should().Be(1, "Edge synthesizer không được gọi khi đang trong thời gian cooldown");

        // 3. Reset thủ công qua ResetEdgeCooldown
        ResilientTextToSpeechService.ResetEdgeCooldown();
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse("Sau khi gọi ResetEdgeCooldown, IsEdgeInCooldown phải là false");

        // 4. Khi Edge thành công, tự động xóa cooldown và trả về kết quả Edge
        var successService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) => Task.FromResult(sampleMp3),
            enableCooldownForCustom: true);

        var successResult = await successService.SynthesizeSpeechAsync("Câu hỏi thành công trên Edge", "vi-VN-HoaiMyNeural", 1.0);
        successResult.UsedFallback.Should().BeFalse();
        successResult.Provider.Should().Be("EdgeNeuralTts");
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();
    }

    [Fact(DisplayName = "29. ResilientTextToSpeechService: Hỗ trợ đầy đủ 2 giọng giám khảo Cô Hoài My (Nữ) và Thầy Nam Minh (Nam) với audio hợp lệ và nhận diện alias")]
    public async Task SynthesizeSpeechAsync_WithDualVoices_SynthesizesDistinctValidAudioForHoaiMyAndNamMinh()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var femaleAudioBytes = new byte[] { 0x49, 0x44, 0x33, 0x01, 0x02, 0x03 };
        var maleAudioBytes = new byte[] { 0x49, 0x44, 0x33, 0x09, 0x08, 0x07 };

        string? capturedVoice = null;
        string? capturedText = null;

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                capturedText = text;
                capturedVoice = voice;
                var audio = voice == ResilientTextToSpeechService.DefaultMaleVoice
                    ? maleAudioBytes
                    : femaleAudioBytes;
                return Task.FromResult(audio);
            });

        // 1. Kiểm tra giọng Nữ mặc định (Cô Hoài My) khi voiceName = null
        var femaleResultDefault = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi thi vấn đáp từ giám khảo nữ",
            voiceName: null,
            speakingRate: 1.0);

        femaleResultDefault.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
        femaleResultDefault.Provider.Should().Be("EdgeNeuralTts");
        femaleResultDefault.AudioBytes.Should().BeEquivalentTo(femaleAudioBytes);
        capturedVoice.Should().Be("vi-VN-HoaiMyNeural");

        // 2. Kiểm tra giọng Nữ khi truyền alias "female"
        var femaleResultAlias = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi tiếp theo từ giám khảo nữ",
            voiceName: "female",
            speakingRate: 1.0);

        femaleResultAlias.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
        capturedVoice.Should().Be("vi-VN-HoaiMyNeural");

        // 3. Kiểm tra giọng Nam (Thầy Nam Minh) khi truyền voiceName chính thức "vi-VN-NamMinhNeural"
        var maleResultExplicit = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi thi vấn đáp từ giám khảo nam",
            voiceName: "vi-VN-NamMinhNeural",
            speakingRate: 1.0);

        maleResultExplicit.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        maleResultExplicit.AudioBytes.Should().BeEquivalentTo(maleAudioBytes);
        capturedVoice.Should().Be("vi-VN-NamMinhNeural");

        // 4. Kiểm tra giọng Nam khi truyền alias "nam" hoặc "male"
        var maleResultAlias = await ttsService.SynthesizeSpeechAsync(
            "Câu hỏi bổ sung từ giám khảo nam",
            voiceName: "nam",
            speakingRate: 1.0);

        maleResultAlias.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        maleResultAlias.AudioBytes.Should().BeEquivalentTo(maleAudioBytes);
        capturedVoice.Should().Be("vi-VN-NamMinhNeural");
    }

    [Fact(DisplayName = "30. ResilientTextToSpeechService: Bộ nhớ đệm SHA-256 phân biệt chính xác theo giọng đọc, tốc độ và chuẩn hóa thuật ngữ SE")]
    public async Task SynthesizeSpeechAsync_Sha256Cache_DifferentiatesVoicesAndRatesAndNormalizesSeMarkdown()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var sampleAudio = new byte[] { 0x49, 0x44, 0x33, 0xAA, 0xBB, 0xCC };
        int synthInvocationCount = 0;
        string? lastSynthesizedText = null;

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                lastSynthesizedText = text;
                Interlocked.Increment(ref synthInvocationCount);
                return Task.FromResult(sampleAudio);
            });

        // 1. Phân biệt theo Giọng đọc: Cùng nội dung + cùng tốc độ nhưng khác giọng -> CacheKey phải khác nhau
        var resultHoaiMy = await ttsService.SynthesizeSpeechAsync(
            "Trình bày cấu trúc 4 tầng của Clean Architecture.",
            "vi-VN-HoaiMyNeural",
            1.0);

        var resultNamMinh = await ttsService.SynthesizeSpeechAsync(
            "Trình bày cấu trúc 4 tầng của Clean Architecture.",
            "vi-VN-NamMinhNeural",
            1.0);

        resultHoaiMy.CacheKeySha256.Should().NotBe(resultNamMinh.CacheKeySha256,
            "Cùng câu hỏi nhưng 2 giọng đọc khác nhau phải tạo ra 2 cache key SHA-256 riêng biệt");
        resultHoaiMy.FromCache.Should().BeFalse();
        resultNamMinh.FromCache.Should().BeFalse();
        synthInvocationCount.Should().Be(2);

        // 2. Phân biệt theo Tốc độ đọc: Cùng nội dung + cùng giọng nhưng tốc độ 1.0 vs 1.25 -> CacheKey phải khác nhau
        var resultRate125 = await ttsService.SynthesizeSpeechAsync(
            "Trình bày cấu trúc 4 tầng của Clean Architecture.",
            "vi-VN-HoaiMyNeural",
            1.25);

        resultRate125.CacheKeySha256.Should().NotBe(resultHoaiMy.CacheKeySha256,
            "Tốc độ đọc khác nhau phải sinh ra cache key SHA-256 khác nhau");
        resultRate125.FromCache.Should().BeFalse();
        synthInvocationCount.Should().Be(3);

        // 3. Chuẩn hóa SE Acronyms và Bất biến trước Markdown:
        var resultPlain = await ttsService.SynthesizeSpeechAsync(
            "Giải thích nguyên lý Dependency Inversion trong C# và .NET",
            "vi-VN-HoaiMyNeural",
            1.0);

        resultPlain.FromCache.Should().BeFalse();
        synthInvocationCount.Should().Be(4);
        lastSynthesizedText.Should().Contain("C sharp");
        lastSynthesizedText.Should().Contain("dot NET");

        var resultWithMarkdown = await ttsService.SynthesizeSpeechAsync(
            "### Giải thích nguyên lý **Dependency Inversion** trong `C#` và `.NET`",
            "vi-VN-HoaiMyNeural",
            1.0);

        resultWithMarkdown.FromCache.Should().BeTrue("Văn bản có định dạng Markdown sau chuẩn hóa trùng cleanedText phải nhận được kết quả từ cache");
        resultWithMarkdown.CacheKeySha256.Should().Be(resultPlain.CacheKeySha256);
        synthInvocationCount.Should().Be(4, "Không được gọi lại bộ tổng hợp âm thanh khi đã có trong cache SHA-256");

        // 4. Kiểm tra bảo vệ: Đầu vào chỉ toàn ký hiệu Markdown bị làm sạch thành chuỗi rỗng phải ném ArgumentException
        var actOnlyMarkdown = () => ttsService.SynthesizeSpeechAsync("### ** `` ~~", "vi-VN-HoaiMyNeural", 1.0);
        await actOnlyMarkdown.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*không được để trống*");
    }

    [Fact(DisplayName = "31. CloudflareWhisperService: Xử lý an toàn khi response chứa các thuộc tính số bị định dạng sai kiểu (string hoặc null) thay vì ném ngoại lệ")]
    public async Task TranscribeDetailedAsync_WhenWhisperResponseContainsNonNumberMetrics_DoesNotThrowAndParsesSafely()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-123",
            ["Cloudflare:ApiToken"] = "cf-token-456"
        });

        var malformedJsonResponse = """
        {
            "result": {
                "text": "Kiểm tra bảo vệ kiểu số Whisper",
                "segments": [
                    {
                        "id": 0,
                        "text": "Kiểm tra bảo vệ kiểu số Whisper",
                        "no_speech_prob": "invalid_string_prob",
                        "avg_logprob": "invalid_string_logprob",
                        "start": "zero",
                        "end": null
                    }
                ]
            }
        }
        """;

        var whisperHandler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(malformedJsonResponse, Encoding.UTF8, "application/json")
            });
        });

        var whisperService = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-audio-bytes"));

        var result = await whisperService.TranscribeDetailedAsync(stream, "test.webm");

        result.Should().NotBeNull();
        result.Text.Should().Be("Kiểm tra bảo vệ kiểu số Whisper");
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore.Should().BeInRange(0.0, 1.0);
    }

    [Fact(DisplayName = "32. Chặn phục hồi ảo giác tiếng ồn (Thank you very much) trong ClientTranscript với TranscribeSpeechCommandHandler và UploadAudioPracticeCommandHandler")]
    public async Task TranscribeAndUploadAudio_WhenClientTranscriptIsNoiseHallucination_RejectsAndDoesNotResurrect()
    {
        var hallucination = "Thank you very much. Thank you very much.";

        // 1. Kiểm thử TranscribeSpeechCommandHandler
        var emptyConfig = BuildConfig(new Dictionary<string, string?>());
        var primaryWhisper = new CloudflareWhisperService(new HttpClient(), emptyConfig, CreateFastNoRetryPipeline());
        var fallbackGemini = new GeminiSpeechToTextService(new HttpClient(), emptyConfig);
        var resilientStt = new ResilientSpeechToTextService(primaryWhisper, fallbackGemini);
        var transcribeHandler = new TranscribeSpeechCommandHandler(resilientStt);

        using var audioStream1 = new MemoryStream(Encoding.UTF8.GetBytes("empty-or-unparseable-audio"));
        var transcribeResult = await transcribeHandler.Handle(
            new TranscribeSpeechCommand(
                audioStream1,
                "answer.webm",
                VocabularyHint: null,
                ClientTranscript: hallucination),
            CancellationToken.None);

        transcribeResult.IsSuccess.Should().BeFalse("Ảo giác tiếng ồn trong ClientTranscript không được phép phục hồi thành công");
        transcribeResult.Error.Should().Contain("Không nhận diện được nội dung");

        // 2. Kiểm thử UploadAudioPracticeCommandHandler
        var uploadHandler = new UploadAudioPracticeCommandHandler(resilientStt);
        using var audioStream2 = new MemoryStream(Encoding.UTF8.GetBytes("empty-or-unparseable-audio"));
        var uploadResult = await uploadHandler.Handle(
            new UploadAudioPracticeCommand(
                audioStream2,
                "practice.webm",
                "audio/webm",
                ClientTranscript: hallucination),
            CancellationToken.None);

        uploadResult.IsSuccess.Should().BeFalse("Ảo giác tiếng ồn trong ClientTranscript không được phép trả về kết quả hợp lệ trong UploadAudioPractice");
    }

    [Fact(DisplayName = "33. GeminiSpeechToTextService: Trả về ConfidenceScore = 0.0 và chuỗi rỗng khi Gemini trả về ảo giác tiếng ồn hoặc văn bản rỗng")]
    public async Task GeminiSpeechToTextService_WhenGeminiReturnsHallucinationOrEmpty_ReturnsZeroConfidenceAndEmptyText()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "fake-gemini-key"
        });

        // Kịch bản 1: Gemini trả về ảo giác lặp tiếng ồn
        var hallucinationJson = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Thank you very much. Thank you very much. Thank you very much.\"}]}}]}";
        var hallucinationHandler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(hallucinationJson, Encoding.UTF8, "application/json")
            });
        });

        var geminiService1 = new GeminiSpeechToTextService(new HttpClient(hallucinationHandler), config);
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("noisy-audio"));
        var result1 = await geminiService1.TranscribeDetailedAsync(stream1, "audio.webm");

        result1.Should().NotBeNull();
        result1.Text.Should().BeEmpty("Ảo giác tiếng ồn phải bị làm sạch thành chuỗi rỗng");
        result1.RawText.Should().BeEmpty();
        result1.ConfidenceScore.Should().Be(0.0, "Độ tin cậy phải được hiệu chuẩn về 0.0 khi phát hiện ảo giác");

        // Kịch bản 2: Gemini trả về văn bản rỗng
        var emptyJson = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"   \"}]}}]}";
        var emptyHandler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptyJson, Encoding.UTF8, "application/json")
            });
        });

        var geminiService2 = new GeminiSpeechToTextService(new HttpClient(emptyHandler), config);
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("silent-audio"));
        var result2 = await geminiService2.TranscribeDetailedAsync(stream2, "audio.webm");

        result2.Should().NotBeNull();
        result2.Text.Should().BeEmpty();
        result2.RawText.Should().BeEmpty();
        result2.ConfidenceScore.Should().Be(0.0, "Độ tin cậy phải là 0.0 khi văn bản bóc băng rỗng");
    }

    [Fact(DisplayName = "34. SynthesizeSpeechQueryValidator chấp nhận các tốc độ đọc chuẩn giao diện [0.5x, 2.0x] bao gồm 0.5x và 1.5x")]
    public void SynthesizeSpeechQueryValidator_AcceptsStandardPlaybackSpeeds_HalfToTwo()
    {
        var validator = new SynthesizeSpeechQueryValidator();

        var queryHalf = new SynthesizeSpeechQuery("Kiểm tra tốc độ đọc 0.5x", SpeakingRate: 0.5);
        var queryOneAndHalf = new SynthesizeSpeechQuery("Kiểm tra tốc độ đọc 1.5x", SpeakingRate: 1.5);
        var queryDouble = new SynthesizeSpeechQuery("Kiểm tra tốc độ đọc 2.0x", SpeakingRate: 2.0);

        var resultHalf = validator.Validate(queryHalf);
        var resultOneAndHalf = validator.Validate(queryOneAndHalf);
        var resultDouble = validator.Validate(queryDouble);

        resultHalf.IsValid.Should().BeTrue("Tốc độ đọc 0.5x phải hợp lệ trong validator");
        resultOneAndHalf.IsValid.Should().BeTrue("Tốc độ đọc 1.5x phải hợp lệ trong validator");
        resultDouble.IsValid.Should().BeTrue("Tốc độ đọc 2.0x phải hợp lệ trong validator");

        // Kiểm tra ngưỡng biên ngoài khoảng [0.5, 2.0]
        var queryTooSlow = new SynthesizeSpeechQuery("Quá chậm", SpeakingRate: 0.49);
        var queryTooFast = new SynthesizeSpeechQuery("Quá nhanh", SpeakingRate: 2.01);

        validator.Validate(queryTooSlow).IsValid.Should().BeFalse("Tốc độ đọc < 0.5x phải bị từ chối");
        validator.Validate(queryTooFast).IsValid.Should().BeFalse("Tốc độ đọc > 2.0x phải bị từ chối");
    }

    [Theory(DisplayName = "35. TranscriptHelper.IsNoiseHallucination nhận diện chính xác các biến thể ảo giác tiếng ồn (unspaced, comma, newline, loop) và không chặn câu trả lời hợp lệ")]
    [InlineData("Thank you very much. Thank you very much.", true)]
    [InlineData("Thank you very much.Thank you very much.", true)]
    [InlineData("Thank you very much, thank you very much", true)]
    [InlineData("Thank you very much\nThank you very much", true)]
    [InlineData("Thank you very much Thank you very much Thank you very much", true)]
    [InlineData("Thank you very much. Please subscribe. Like and subscribe.", true)]
    [InlineData("Hãy đăng ký kênh và bấm chuông theo dõi", true)]
    [InlineData("Các bạn hãy đăng kí cho kênh lalaschool", true)]
    [InlineData("lalaschool", true)]
    [InlineData("Ghiền Mì Gõ", true)]
    [InlineData("Cảm ơn các bạn đã theo dõi. Hẹn gặp lại các bạn.", true)]
    [InlineData("Thank you very much. Em xin trả lời câu hỏi về Clean Architecture.", false)]
    [InlineData("Clean Architecture gồm 4 tầng Domain, Application, Infrastructure và Web API.", false)]
    public void TranscriptHelper_IsNoiseHallucination_EvaluatesEdgeCasesCorrectly(string input, bool expectedIsHallucination)
    {
        var actual = TranscriptHelper.IsNoiseHallucination(input);
        actual.Should().Be(expectedIsHallucination, $"Input '{input}' phải được đánh giá ảo giác tiếng ồn = {expectedIsHallucination}");

        // Kiểm thử NormalizeOrFallback khi mock trả về null (unconfigured mock)
        var unconfiguredFallback = TranscriptHelper.NormalizeOrFallback(input, null);
        if (expectedIsHallucination)
        {
            unconfiguredFallback.Should().BeEmpty("Ảo giác tiếng ồn không được phép trả về kết quả khi mock trả về null");
        }
        else
        {
            unconfiguredFallback.Should().Be(input.Trim(), "Nội dung hợp lệ phải được fallback về rawText khi mock trả về null");
        }

        // Kiểm thử NormalizeOrFallback khi STT service đã strip thành chuỗi rỗng
        var strippedFallback = TranscriptHelper.NormalizeOrFallback(input, string.Empty);
        strippedFallback.Should().BeEmpty("Khi STT service đã làm sạch thành rỗng thì cấm hồi sinh ảo giác");
    }

    [Fact(DisplayName = "36. Moq STT chưa cấu hình (NormalizeTranscript trả về null): từ chối ảo giác và giữ lại câu trả lời hợp lệ")]
    public async Task TranscribeAndUploadAudio_WithUnconfiguredMockReturningNull_HandlesCorrectly()
    {
        var unconfiguredMock = new Mock<ISpeechToTextService>();
        // Mock không cài đặt NormalizeTranscript -> trả về null mặc định
        unconfiguredMock.Setup(s => s.TranscribeDetailedAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpeechTranscriptionResult(string.Empty, string.Empty, "MockStt", false, null, 0.0));

        // 1. TranscribeSpeechCommandHandler với ClientTranscript là ảo giác
        var transcribeHandler = new TranscribeSpeechCommandHandler(unconfiguredMock.Object);
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("audio-1"));
        var noiseResult = await transcribeHandler.Handle(
            new TranscribeSpeechCommand(stream1, "test.webm", null, "Thank you very much.Thank you very much."),
            CancellationToken.None);
        noiseResult.IsSuccess.Should().BeFalse("TranscribeSpeechCommandHandler phải từ chối khi ClientTranscript là ảo giác tiếng ồn dù mock trả về null");

        // 2. TranscribeSpeechCommandHandler với ClientTranscript hợp lệ
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("audio-2"));
        var validResult = await transcribeHandler.Handle(
            new TranscribeSpeechCommand(stream2, "test.webm", null, "Em áp dụng Dependency Inversion"),
            CancellationToken.None);
        validResult.IsSuccess.Should().BeTrue("TranscribeSpeechCommandHandler phải chấp nhận ClientTranscript hợp lệ khi mock trả về null");
        validResult.Value!.Text.Should().Be("Em áp dụng Dependency Inversion");

        // 3. UploadAudioPracticeCommandHandler với ClientTranscript là ảo giác lặp
        var uploadHandler = new UploadAudioPracticeCommandHandler(unconfiguredMock.Object);
        using var stream3 = new MemoryStream(Encoding.UTF8.GetBytes("audio-3"));
        var uploadNoiseResult = await uploadHandler.Handle(
            new UploadAudioPracticeCommand(stream3, "test.webm", "audio/webm", "Thank you very much, thank you very much"),
            CancellationToken.None);
        uploadNoiseResult.IsSuccess.Should().BeFalse("UploadAudioPracticeCommandHandler phải từ chối ảo giác lặp tiếng ồn");

        // 4. UploadAudioPracticeCommandHandler với ClientTranscript hợp lệ
        using var stream4 = new MemoryStream(Encoding.UTF8.GetBytes("audio-4"));
        var uploadValidResult = await uploadHandler.Handle(
            new UploadAudioPracticeCommand(stream4, "test.webm", "audio/webm", "CQRS tách biệt Command và Query"),
            CancellationToken.None);
        uploadValidResult.IsSuccess.Should().BeTrue("UploadAudioPracticeCommandHandler phải chấp nhận ClientTranscript hợp lệ");
        uploadValidResult.Value!.Transcript.Should().Be("CQRS tách biệt Command và Query");
    }

    [Fact(DisplayName = "37. SubmitMockExamAnswerCommandHandler từ chối khi DirectTranscript là ảo giác tiếng ồn")]
    public async Task SubmitMockExamAnswerCommandHandler_WhenDirectTranscriptIsNoiseHallucination_Rejects()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(25),
            Status = "in_progress"
        };

        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionId,
            Status = "pending",
            TimeTakenSeconds = 0,
            AnswerText = null
        });

        // Include(Course) bắt buộc — thiếu Course thì session bị lọc mất (INNER JOIN).
        context.Courses.Add(new Course
        {
            Id = courseId,
            Code = "PRN231",
            Name = "Mock Exam STT Test",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            MaxFollowUpQuestions = 2,
            IsActive = true,
        });
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        var sttMock = new Mock<ISpeechToTextService>();
        // NormalizeTranscript trả về chuỗi rỗng khi gặp ảo giác
        sttMock.Setup(s => s.NormalizeTranscript(It.IsAny<string>()))
            .Returns<string>(s => TranscriptHelper.IsNoiseHallucination(s) ? string.Empty : s.Trim());

        var handler = new SubmitMockExamAnswerCommandHandler(context, sttMock.Object);

        // Nộp DirectTranscript dạng ảo giác tiếng ồn
        var command = new SubmitMockExamAnswerCommand(
            SessionId: session.Id,
            StudentId: studentId,
            QuestionId: questionId,
            AudioStream: null,
            FileName: null,
            ContentType: null,
            TimeTakenSeconds: 15,
            DirectTranscript: "Thank you very much. Thank you very much."
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse("DirectTranscript là ảo giác tiếng ồn bắt buộc phải bị từ chối");
        result.Error.Should().Contain("Không nhận diện được giọng nói rõ ràng (hoặc chỉ có tiếng ồn nền)");
    }

    [Theory(DisplayName = "38. SeTerminologyNormalizer.SanitizeHallucinations và IsHallucinationOrNoise xử lý chuẩn mọi biến thể và bảo toàn câu trả lời kỹ thuật")]
    [InlineData("Thank you very much.Thank you very much.", "", true)]
    [InlineData("Thank you very much, thank you very much", "", true)]
    [InlineData("Thank you very much\nThank you very much", "", true)]
    [InlineData("Thank you very much Thank you very much Thank you very much", "", true)]
    [InlineData("Thank you very much. Please subscribe. Like and subscribe.", "", true)]
    [InlineData("Hãy đăng ký kênh và bấm chuông theo dõi", "", true)]
    [InlineData("Các bạn hãy đăng kí cho kênh lalaschool", "", true)]
    [InlineData("lalaschool", "", true)]
    [InlineData("Ghiền Mì Gõ", "", true)]
    [InlineData("Cảm ơn các bạn đã theo dõi. Hẹn gặp lại các bạn.", "", true)]
    [InlineData("bye bye bye", "", true)]
    [InlineData("you you you", "", true)]
    [InlineData("Thank you very much. Em xin trả lời câu hỏi về Clean Architecture.", "Em xin trả lời câu hỏi về Clean Architecture.", false)]
    [InlineData("Clean Architecture gồm 4 tầng Domain, Application, Infrastructure và Web API.", "Clean Architecture gồm 4 tầng Domain, Application, Infrastructure và Web API.", false)]
    [InlineData("Chúng ta phân tích step by step, byte by byte và point to point.", "Chúng ta phân tích step by step, byte by byte và point to point.", false)]
    [InlineData("Quan hệ table of table trong cơ sở dữ liệu.", "Quan hệ table of table trong cơ sở dữ liệu.", false)]
    public void SeTerminologyNormalizer_SanitizeHallucinations_HandlesEdgeCasesAndPreservesTechnicalContent(
        string input, string expectedSanitized, bool expectedIsHallucination)
    {
        var sanitized = SeTerminologyNormalizer.SanitizeHallucinations(input);
        sanitized.Should().Be(expectedSanitized, $"SanitizeHallucinations cho '{input}' phải ra '{expectedSanitized}'");

        var isNoise = SeTerminologyNormalizer.IsHallucinationOrNoise(input);
        isNoise.Should().Be(expectedIsHallucination, $"IsHallucinationOrNoise cho '{input}' phải là {expectedIsHallucination}");
    }

    [Theory(DisplayName = "39. GeminiSpeechToTextService: Trả về ConfidenceScore = 0.0 và chuỗi rỗng trên các biến thể ảo giác (unspaced, comma, newline) và audio rỗng")]
    [InlineData("Thank you very much.Thank you very much.")]
    [InlineData("Thank you very much, thank you very much")]
    [InlineData("Thank you very much\nThank you very much")]
    [InlineData("Please subscribe. Like and subscribe.")]
    public async Task GeminiSpeechToTextService_WithVariousHallucinations_ReturnsZeroConfidenceAndEmpty(string hallucination)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "fake-gemini-key"
        });

        var hallucinationJson = $"{{\"candidates\":[{{\"content\":{{\"parts\":[{{\"text\":\"{hallucination.Replace("\n", "\\n")}\"}}]}}}}]}}";
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(hallucinationJson, Encoding.UTF8, "application/json")
            });
        });

        var service = new GeminiSpeechToTextService(new HttpClient(handler), config);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("noisy-audio"));
        var result = await service.TranscribeDetailedAsync(stream, "audio.webm");

        result.Should().NotBeNull();
        result.Text.Should().BeEmpty();
        result.RawText.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(0.0);
    }

    [Fact(DisplayName = "40. TranscriptHelper.NormalizeOrFallback chặn triệt để cả trường hợp mock trả về ảo giác nguyên vẹn")]
    public void TranscriptHelper_NormalizeOrFallback_BlocksUnsanitizedNormalizedOutput()
    {
        // Khi mock/service trả về ảo giác mà chưa lọc sạch
        var result = TranscriptHelper.NormalizeOrFallback("Thank you very much.Thank you very much.", "Thank you very much.Thank you very much.");
        result.Should().BeEmpty("NormalizeOrFallback không được phép trả về ảo giác dù normalized khác null");

        // Khi normalized là hợp lệ
        var validResult = TranscriptHelper.NormalizeOrFallback("Em áp dụng CQRS", "Em áp dụng CQRS");
        validResult.Should().Be("Em áp dụng CQRS");
    }

    [Fact(DisplayName = "41. Regex khử ảo giác và bóc tách câu có khả năng chống ReDoS với giới hạn thời gian thực thi")]
    public void HallucinationRegex_IsResistantToCatastrophicBacktracking()
    {
        var longRepeatingText = string.Join(" ", Enumerable.Repeat("test", 1000));
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var isNoiseHelper = TranscriptHelper.IsNoiseHallucination(longRepeatingText);
        var sanitizedNormalizer = SeTerminologyNormalizer.SanitizeHallucinations(longRepeatingText);

        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Regex khử ảo giác phải xử lý chuỗi lặp dài trong vòng 2 giây mà không bị ReDoS");
        isNoiseHelper.Should().BeFalse();
        sanitizedNormalizer.Should().Be("test");
    }

    [Fact(DisplayName = "42. Khắc phục 5 lỗ hổng STT/TTS: bảo toàn từ tiếng Việt đời thường, nhấn mạnh 3 từ, giữ vocabularyHint tiếng Anh và bảo toàn mũi tên ASCII")]
    public void SeTerminologyNormalizer_FiveSpeechVulnerabilities_PreservesEverydayVietnameseAndEnglishHintsAndArrows()
    {
        // Lỗ hổng 3: Không biến từ tiếng Việt thông thường thành thuật ngữ tiếng Anh, và không cắt cụm nhấn mạnh 3 từ
        var everydayVi = "Em cần in một đơn hàng rất rất rất quan trọng và áp lai sơ tuyển";
        var normalizedVi = SeTerminologyNormalizer.Normalize(everydayVi);
        normalizedVi.Should().Contain("in một đơn hàng");
        normalizedVi.Should().NotContain("In-Memory");
        normalizedVi.Should().Contain("rất rất rất quan trọng");
        normalizedVi.Should().NotContain("Apply");

        // Lỗ hổng 4: BuildContextualWhisperPrompt giữ nguyên từ khóa tiếng Anh và trả chuỗi rỗng khi null hoặc lang:en
        SeTerminologyNormalizer.BuildContextualWhisperPrompt(null).Should().BeEmpty();
        SeTerminologyNormalizer.BuildContextualWhisperPrompt("lang:en").Should().BeEmpty();
        var englishHintPrompt = SeTerminologyNormalizer.BuildContextualWhisperPrompt("SOLID, Microservices, CQRS");
        englishHintPrompt.Should().Contain("SOLID, Microservices, CQRS");
        englishHintPrompt.Should().NotContain("Dạ vâng, em xin trả lời");

        // Lỗ hổng 2: BuildCustomVocabularyList trả rỗng khi không có hint hoặc lang:en
        SeTerminologyNormalizer.BuildCustomVocabularyList(null).Should().BeEmpty();
        SeTerminologyNormalizer.BuildCustomVocabularyList("lang:en").Should().BeEmpty();

        // Lỗ hổng 5: PrepareTextForTts bảo toàn mũi tên <=>, =>, <== và từ kết thúc bằng c#
        var ttsArrows = SeTerminologyNormalizer.PrepareTextForTts("Biểu thức A <=> B và x => y với điểm >= 5 và <= 10 trong C# (không phải abc#)");
        ttsArrows.Should().Contain("<=>");
        ttsArrows.Should().Contain("=>");
        ttsArrows.Should().Contain("lớn hơn hoặc bằng 5");
        ttsArrows.Should().Contain("nhỏ hơn hoặc bằng 10");
        ttsArrows.Should().Contain("C sharp");
        ttsArrows.Should().Contain("abc");
        ttsArrows.Should().NotContain("abc sharp");
    }

    [Fact(DisplayName = "43. Khắc phục Lỗi 7 & 8: IsEnglishCourse không nhận nhầm câu hỏi SE viết bằng tiếng Anh thành môn ENT và ThrowOnTransientFailure ném lỗi HTTP 429 cho DLQ")]
    public async Task GeminiAiGradingService_CourseClassificationAndTransientFailure_BehavesCorrectly()
    {
        OralExamination.Application.Common.Interfaces.FollowUpPromptBuilder.IsEnglishCourse(
            "SWD392",
            null,
            "Explain SOLID principles and Dependency Injection in .NET 8?").Should().BeFalse();
        OralExamination.Application.Common.Interfaces.FollowUpPromptBuilder.IsEnglishCourse(
            "ENT203",
            null,
            "Describe the picture in detail.").Should().BeTrue();

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "AIzaSy_TestKey_RateLimit"
        });
        var rateLimitHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(@"{""error"":{""message"":""Resource has been exhausted""}}", Encoding.UTF8, "application/json")
            }));
        var gradingService = new GeminiAiGradingService(new HttpClient(rateLimitHandler), config);
        var request = new OralExamination.Application.Common.Interfaces.AiGradingRequest
        {
            CourseCode = "SWD392",
            QuestionContent = "Explain Clean Architecture",
            SampleAnswer = "Domain, Application, Infrastructure, API",
            Transcript = "Clean Architecture separates Domain and Application from Infrastructure.",
            ThrowOnTransientFailure = true
        };

        Func<Task> act = () => gradingService.GradeAnswerAsync(request);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact(DisplayName = "44. GeminiAiGradingService: ThrowOnTransientFailure mặc định bằng true và System Prompt cho phép sinh FollowUpPrompt khi Score > 0.0 (phục vụ MF-02)")]
    public async Task GeminiAiGradingService_PromptAllowsFollowUpForAnyPositiveScore_AndDefaultsThrowOnTransientFailure()
    {
        var defaultRequest = new OralExamination.Application.Common.Interfaces.AiGradingRequest();
        defaultRequest.ThrowOnTransientFailure.Should().BeTrue();

        string? capturedPayload = null;
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "AIzaSy_TestKey_PromptCheck"
        });
        var captureHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedPayload = req.Content != null
                ? await req.Content.ReadAsStringAsync(ct)
                : string.Empty;
            var geminiJson = @"{
                ""candidates"": [{
                    ""content"": {
                        ""parts"": [{
                            ""text"": ""{\""Score\"": 2.5, \""ConfidenceScore\"": 0.92, \""IsSuspicious\"": false, \""Feedback\"": \""Còn thiếu nhiều ý.\"", \""CotTrace\"": \""Sinh viên mới nêu được khái niệm cơ bản.\"", \""FollowUpPrompt\"": \""Bạn vừa nhắc đến tầng Domain, vậy tại sao tầng Domain không được phụ thuộc vào Infrastructure?\"", \""Details\"": []}""
                        }]
                    }
                }]
            }";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiJson, Encoding.UTF8, "application/json")
            };
        });

        var gradingService = new GeminiAiGradingService(new HttpClient(captureHandler), config);
        var result = await gradingService.GradeAnswerAsync(new OralExamination.Application.Common.Interfaces.AiGradingRequest
        {
            CourseCode = "SWD392",
            QuestionContent = "Trình bày Clean Architecture.",
            SampleAnswer = "Domain, Application, Infrastructure, API.",
            Transcript = "Em nhớ là có tầng Domain chứa các thực thể chính."
        });

        capturedPayload.Should().NotBeNullOrWhiteSpace();
        capturedPayload.Should().NotContain("Nếu Score < 4.0");
        capturedPayload.Should().Contain("Nếu Score == 0.0 hoặc IsSuspicious == true");
        result.Score.Should().Be(2.5m);
        result.FollowUpPrompt.Should().Contain("tầng Domain");
    }

    [Fact(DisplayName = "45. GeminiQuestionGenerationService: Tự động chuyển sang Cloudflare Workers AI Llama-3.1 khi khóa AQ.Ab8... và ném lỗi khi cả 2 AI đều lỗi (không sinh đề văn mẫu giả)")]
    public async Task GeminiQuestionGenerationService_WithAqKey_UsesCloudflareWorkersAiAndThrowsWhenUnavailable()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "AQ.Ab8RN6I_TestVertexKey",
            ["Cloudflare__AccountId"] = "cf-acc-999",
            ["Cloudflare__ApiToken"] = "cf-tok-999"
        });

        var cfLlamaJson = @"{
            ""result"": {
                ""response"": ""[{\""cloCode\"": \""CLO1\"", \""title\"": \""Vai trò của MediatR trong CQRS\"", \""content\"": \""Phân tích vai trò của MediatR trong kiến trúc CQRS .NET 8?\"", \""difficulty\"": \""medium\"", \""bloomLevel\"": \""Analyze\"", \""modelAnswer\"": \""MediatR đóng vai trò In-Process Mediator giúp tách rời hoàn toàn Sender (Controller) và Handler (Application Use Case) thông qua IRequest và IRequestHandler.\"", \""rubric\"": {\""name\"": \""Barem CQRS\"", \""description\"": \""Đánh giá CQRS\"", \""totalMaxScore\"": 10.0, \""criteria\"": [{\""criterionName\"": \""Khái niệm Mediator\"", \""maxScore\"": 5.0, \""weight\"": 50, \""bloomLevel\"": \""Understand\"", \""description\"": \""Nêu đúng nguyên lý In-Process Mediator\"", \""orderIndex\"": 1}, {\""criterionName\"": \""Triển khai IRequest/IRequestHandler\"", \""maxScore\"": 5.0, \""weight\"": 50, \""bloomLevel\"": \""Analyze\"", \""description\"": \""Giải thích luồng xử lý Command/Query\"", \""orderIndex\"": 2}]}}]""
            }
        }";

        var successHandler = new MockHttpMessageHandler((req, ct) =>
        {
            if (req.RequestUri!.Host.Contains("generativelanguage.googleapis.com")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)); req.RequestUri!.ToString().Should().Contain("@cf/meta/llama-3.1-8b-instruct");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(cfLlamaJson, Encoding.UTF8, "application/json")
            });
        });

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiQuestionGenerationService>.Instance;
        var genService = new GeminiQuestionGenerationService(config, logger, new HttpClient(successHandler));
        var genParams = new OralExamination.Application.Common.Interfaces.AiQuestionGenerationParams(
            CourseCode: "SWD392",
            CourseName: "Kiến trúc phần mềm",
            SelectedCloCodes: new List<string> { "CLO1" },
            Topics: new List<string> { "CQRS & Clean Architecture" },
            NumberOfQuestions: 1,
            UsageScope: "both",
            TargetBloomLevels: new List<string> { "Analyze" },
            EasyCount: 0,
            MediumCount: 1,
            HardCount: 0);

        var generated = await genService.GenerateQuestionsAsync(genParams);

        generated.Should().ContainSingle();
        generated[0].Content.Should().Contain("MediatR");
        generated[0].Rubric.Criteria.Sum(c => c.MaxScore).Should().Be(10.0m);

        var failHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var failingService = new GeminiQuestionGenerationService(config, logger, new HttpClient(failHandler));
        Func<Task> act = () => failingService.GenerateQuestionsAsync(genParams);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact(DisplayName = "46. Phục hồi bài thi thử MF-02 khi mất mạng/tắt trình duyệt: GetMockExamHistory bảo toàn trạng thái submitted (không xóa về 0đ) và GetMockExamSessionDetail tự động chấm bù")]
    public async Task MockExamDisconnectRecovery_PreservesAnsweredQuestionsAndLazilyGradesOnDetailView()
    {
        using var context = CreateDbContext();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var criterionId = Guid.NewGuid();

        var course = new Course
        {
            Id = courseId,
            Code = "SWD392",
            Name = "Software Architecture",
            Credits = 3,
            SemesterId = Guid.NewGuid(),
            MaxFollowUpQuestions = 2,
            IsActive = true
        };
        var rubric = new Rubric
        {
            Id = rubricId,
            CourseId = courseId,
            Name = "Barem Clean Architecture",
            TotalMaxScore = 10.0m,
            Criteria = new List<RubricCriterion>
            {
                new()
                {
                    Id = criterionId,
                    RubricId = rubricId,
                    CriterionName = "Nguyên lý phụ thuộc",
                    Description = "Nêu đúng DIP",
                    MaxScore = 10.0m,
                    Weight = 1.0m
                }
            }
        };
        var question = new PracticeQuestion
        {
            Id = questionId,
            CourseId = courseId,
            RubricId = rubricId,
            Title = "Dependency Inversion",
            Content = "Trình bày nguyên lý Dependency Inversion trong Clean Architecture.",
            SampleAnswer = "Các tầng bên trong (Domain, Application) định nghĩa Interfaces, còn tầng Infrastructure triển khai Interfaces đó.",
            Difficulty = "medium",
            IsActive = true
        };

        var session = new MockExamSession
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CourseId = courseId,
            ExamSetId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Đã quá giờ do sinh viên bị mất mạng/tắt máy
            Status = "in_progress",
            HasFollowUp = false
        };

        session.Answers.Add(new MockExamAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            QuestionId = questionId,
            Status = "answered",
            TimeTakenSeconds = 45,
            AnswerText = "Tầng Application định nghĩa các interface và tầng Infrastructure implement chúng để đảo ngược sự phụ thuộc."
        });

        context.Courses.Add(course);
        context.Rubrics.Add(rubric);
        context.PracticeQuestions.Add(question);
        context.MockExamSessions.Add(session);
        await context.SaveChangesAsync();

        // 1. Khi mở trang Lịch sử thi thử: phiên đã trả lời nhưng quá giờ phải chuyển sang "submitted" chờ chấm bù, KHÔNG được gán 0.0đ
        var historyHandler = new OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory.GetMockExamHistoryQueryHandler(context);
        var historyResult = await historyHandler.Handle(
            new OralExamination.Application.Features.MockExams.Queries.GetMockExamHistory.GetMockExamHistoryQuery(studentId),
            CancellationToken.None);

        historyResult.IsSuccess.Should().BeTrue();
        historyResult.Value.Items.Should().ContainSingle();
        historyResult.Value.Items[0].Status.Should().Be("submitted");
        historyResult.Value.Items[0].TotalScore.Should().BeNull();

        // 2. Khi mở chi tiết Scorecard (hoặc khi Worker chạy): tự động gọi AI chấm bù câu đã lưu và chốt điểm thực tế
        var aiMock = new Mock<OralExamination.Application.Common.Interfaces.IAiGradingService>();
        aiMock.Setup(a => a.GradeAnswerAsync(It.IsAny<OralExamination.Application.Common.Interfaces.AiGradingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OralExamination.Application.Common.Interfaces.AiGradingResult
            {
                Score = 8.5m,
                ConfidenceScore = 0.94m,
                IsSuspicious = false,
                Feedback = "Trả lời chính xác nguyên lý Dependency Inversion.",
                CotTrace = "Nêu đúng vai trò Application và Infrastructure.",
                Details = new List<OralExamination.Application.Common.Interfaces.GradingDetailDto>
                {
                    new() { CriterionId = criterionId.ToString(), Score = 8.5m, Comment = "Đúng bản chất" }
                }
            });

        var detailHandler = new OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail.GetMockExamSessionDetailQueryHandler(context, aiMock.Object);
        var detailResult = await detailHandler.Handle(
            new OralExamination.Application.Features.MockExams.Queries.GetMockExamSessionDetail.GetMockExamSessionDetailQuery(session.Id, studentId),
            CancellationToken.None);

        detailResult.IsSuccess.Should().BeTrue();
        detailResult.Value.TotalScore.Should().Be(8.5m);
        var reloadedSession = await context.MockExamSessions.FindAsync(session.Id);
        reloadedSession!.Status.Should().Be("graded");
    }

    [Fact(DisplayName = "47. STT Song ngữ Việt-Anh cho môn ENT & Bóc băng câu trả lời dài (> 2 phút): giữ language='vi' chống dịch ép tiếng Việt sang tiếng Anh và tự động tắt vad_filter khi audio lớn")]
    public async Task BilingualEntStt_AndLongAudioVadBypass_WorksAndEliminatesDoubleSttInMockExam()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc-ent",
            ["Cloudflare:ApiToken"] = "cf-tok-ent",
            ["Gemini:ApiKey"] = "gemini-key-ent"
        });

        var capturedPayloads = new List<string>();
        var callCount = 0;
        var whisperHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            callCount++;
            var body = req.Content != null ? await req.Content.ReadAsStringAsync(ct) : string.Empty;
            capturedPayloads.Add(body);

            // Lần 1: Môn ENT203 nói tiếng Việt kết hợp tiếng Anh -> language phải là "vi", task = "transcribe"
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"result\":{\"text\":\"Dạ theo em trong bức tranh này có một gia đình đang picnic ở park.\"}}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            // Lần 2: Audio dài > 250KB -> vad_filter tự động đặt bằng false
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"result\":{\"text\":\"Câu trả lời dài hơn 2 phút được nhận diện đầy đủ không bị lọc rỗng.\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var whisperService = new CloudflareWhisperService(new HttpClient(whisperHandler), config, CreateFastNoRetryPipeline());

        // 1. Kiểm tra môn ENT (hint lang:en) vẫn dùng language = "vi" để nhận diện cả Tiếng Việt lẫn Tiếng Anh
        using var entStream = new MemoryStream(Encoding.UTF8.GetBytes("ent-bilingual-voice-bytes"));
        var entResult = await whisperService.TranscribeDetailedAsync(entStream, "ent.webm", "lang:en");
        entResult.Text.Should().Be("Dạ theo em trong bức tranh này có một gia đình đang picnic ở park.");
        using (var doc1 = System.Text.Json.JsonDocument.Parse(capturedPayloads[0]))
        {
            doc1.RootElement.GetProperty("language").GetString().Should().Be("vi");
            doc1.RootElement.GetProperty("task").GetString().Should().Be("transcribe");
            doc1.RootElement.GetProperty("vad_filter").GetBoolean().Should().BeTrue();
        }

        // 2. Kiểm tra file âm thanh lớn (> 250KB, tương đương > 2 phút) tự động tắt vad_filter để không bị treo 40s hoặc trả chuỗi rỗng
        var largeAudioBytes = new byte[260_000];
        Array.Fill(largeAudioBytes, (byte)0x55);
        using var longStream = new MemoryStream(largeAudioBytes);
        var longResult = await whisperService.TranscribeDetailedAsync(longStream, "long_2min.webm", null);
        longResult.Text.Should().Contain("Câu trả lời dài hơn 2 phút");
        using (var doc2 = System.Text.Json.JsonDocument.Parse(capturedPayloads[1]))
        {
            doc2.RootElement.GetProperty("vad_filter").GetBoolean().Should().BeFalse();
        }
    }

    // -------------------------------------------------------------------------
    // TEST 50: Chuẩn hóa thuật ngữ OOP, Database, DevOps & Contextual VocabularyHint
    // -------------------------------------------------------------------------
    [Fact]
    public void SeTerminologyNormalizer_NormalizesOopDatabaseDevOpsTerms_AndBuildsContextualPrompt()
    {
        var rawTranscript =
            "Trong lập trình hướng đối tượng có 4 tính chất là en cáp su lây sần, in hê ri tần, pô li mo phi dầm và áp trắc sần. " +
            "Khi thiết kế cơ sở dữ liệu pốt gờ re cần đánh in đếch xinh cho pờ rai mơ ri ki và pho rần ki để tránh đét lóc và rây con đi sần. " +
            "Hệ thống triển khai bằng đốc cơ, ku bơ nê tít và xi ai xi đi kèm iu nít tét.";

        var normalized = SeTerminologyNormalizer.NormalizeWithContext(
            rawTranscript,
            "PRO192 - Object-Oriented Programming - Encapsulation, Inheritance, Polymorphism, Abstraction");

        normalized.Should().Contain("Encapsulation");
        normalized.Should().Contain("Inheritance");
        normalized.Should().Contain("Polymorphism");
        normalized.Should().Contain("Abstraction");
        normalized.Should().Contain("PostgreSQL");
        normalized.Should().Contain("Indexing");
        normalized.Should().Contain("Primary Key");
        normalized.Should().Contain("Foreign Key");
        normalized.Should().Contain("Deadlock");
        normalized.Should().Contain("Race Condition");
        normalized.Should().Contain("Docker");
        normalized.Should().Contain("Kubernetes");
        normalized.Should().Contain("CI/CD");
        normalized.Should().Contain("Unit Test");

        var whisperPrompt = SeTerminologyNormalizer.BuildContextualWhisperPrompt(
            "SWD392 - Clean Architecture & CQRS Pattern");
        whisperPrompt.Should().Contain("Clean Architecture");
        whisperPrompt.Should().Contain("Câu hỏi hiện tại: SWD392 - Clean Architecture & CQRS Pattern");

        var customVocab = SeTerminologyNormalizer.BuildCustomVocabularyList(
            "SWD392 - CleanArchitecture & GradingQueueWorker");
        customVocab.Should().Contain("SWD392");
        customVocab.Should().Contain("CleanArchitecture");
        customVocab.Should().Contain("GradingQueueWorker");
    }
}

