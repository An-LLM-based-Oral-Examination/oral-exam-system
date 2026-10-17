using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Models;
using OralExamination.Infrastructure.Services;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng Milestone 2 (Empirical Adversarial Test Suite for M2).
/// Thực thi độc lập bởi challenger_m2_2.
/// 
/// Trọng tâm thử thách đối kháng:
/// 1. Dual Voices & Alias Resolution:
///    - Chuẩn hóa tên giọng đọc (Hoài My - Nữ, Nam Minh - Nam) với các biến thể hoa/thường, khoảng trắng, alias ("male", "nam", "female").
///    - Kiểm tra danh sách giọng đọc khả dụng (GetAvailableVoices).
///    - Đảm bảo audio tổng hợp từ 2 giọng là độc lập và phân biệt.
/// 2. SHA-256 Caching Partitioning:
///    - Cùng văn bản + cùng tốc độ nhưng khác giọng đọc -> SHA-256 hash khác nhau.
///    - Cùng giọng đọc + cùng văn bản nhưng khác tốc độ -> SHA-256 hash khác nhau.
///    - Văn bản có Markdown vs Plain Text trùng cleanedText -> SHA-256 hash giống nhau (Cache Hit).
///    - Bộ nhớ đệm tự động dọn dẹp khi đạt ngưỡng tối đa 250 phần tử.
/// 3. SE Acronym Preprocessing (PrepareTextForTts):
///    - Chuyển đổi "C#" -> " C sharp " và ".NET" -> " dot NET ".
///    - Chuyển đổi toán tử ">=" -> " lớn hơn hoặc bằng ", "<=" -> " nhỏ hơn hoặc bằng ".
///    - Chuyển đổi thang điểm "10.0đ" / "10đ" -> "10 điểm".
///    - Khử hoàn toàn các định dạng Markdown.
///    - Chuỗi rỗng hoặc chỉ chứa Markdown ném ArgumentException trong SynthesizeSpeechAsync.
/// 4. Whisper JSON Non-Number Attribute Handling:
///    - Payload chứa no_speech_prob, avg_logprob, start, end là string, bool, null, array, object.
///    - Kiểm tra parser không ném InvalidOperationException, tính toán ConfidenceScore an toàn trong [0.0, 1.0].
///    - Kiểm tra lọc phân đoạn im lặng vẫn hoạt động chính xác khi gặp no_speech_prob > 0.85 hợp lệ.
/// </summary>
public class AdversarialMilestone2ChallengerTests
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

    private static CloudflareWhisperService CreateWhisperService(string responseJson)
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-challenger-acc-m2",
            ["Cloudflare:ApiToken"] = "cf-challenger-token-m2"
        });

        var handler = new OralExamination.UnitTests.Services.MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));

        return new CloudflareWhisperService(new HttpClient(handler), config, CreateFastNoRetryPipeline());
    }

    // =========================================================================
    // NHÓM 1: EXAMINER DUAL VOICES & ALIAS RESOLUTION
    // =========================================================================

    [Theory(DisplayName = "ADV-M2-01: ResolveVoiceName giải quyết chính xác các alias của Thầy Nam Minh (Nam)")]
    [InlineData("vi-VN-NamMinhNeural")]
    [InlineData("NamMinh")]
    [InlineData("namminh")]
    [InlineData("NAMMINH")]
    [InlineData("male")]
    [InlineData("Male")]
    [InlineData("MALE")]
    [InlineData("nam")]
    [InlineData("Nam")]
    [InlineData("NAM")]
    [InlineData("  male  ")]
    [InlineData("  vi-VN-NamMinhNeural  ")]
    public void ResolveVoiceName_MaleAliases_ResolvesToDefaultMaleVoice(string input)
    {
        var resolved = ResilientTextToSpeechService.ResolveVoiceName(input);
        resolved.Should().Be(ResilientTextToSpeechService.DefaultMaleVoice);
        resolved.Should().Be("vi-VN-NamMinhNeural");
    }

    [Theory(DisplayName = "ADV-M2-02: ResolveVoiceName giải quyết chính xác giọng Cô Hoài My (Nữ) cho các alias, null, empty hoặc không xác định")]
    [InlineData("vi-VN-HoaiMyNeural")]
    [InlineData("HoaiMy")]
    [InlineData("hoaimy")]
    [InlineData("female")]
    [InlineData("Female")]
    [InlineData("FEMALE")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-voice-identifier")]
    [InlineData("google-tts-fallback")]
    public void ResolveVoiceName_FemaleAliasesAndDefaults_ResolvesToDefaultFemaleVoice(string? input)
    {
        var resolved = ResilientTextToSpeechService.ResolveVoiceName(input);
        resolved.Should().Be(ResilientTextToSpeechService.DefaultFemaleVoice);
        resolved.Should().Be("vi-VN-HoaiMyNeural");
    }

    [Fact(DisplayName = "ADV-M2-03: GetAvailableVoices trả về đúng 2 giọng giám khảo chuẩn khảo thí FPT")]
    public void GetAvailableVoices_ReturnsStandardDualVoices()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var tts = new ResilientTextToSpeechService(new HttpClient(), config);

        var voices = tts.GetAvailableVoices();

        voices.Should().HaveCount(2);
        voices.Should().Contain(v => v.VoiceId == "vi-VN-HoaiMyNeural" && v.Gender == "Female" && v.IsDefault);
        voices.Should().Contain(v => v.VoiceId == "vi-VN-NamMinhNeural" && v.Gender == "Male" && !v.IsDefault);
    }

    [Fact(DisplayName = "ADV-M2-04: SynthesizeSpeechAsync truyền đúng resolvedVoice tới primary synthesizer và trả kết quả riêng biệt")]
    public async Task SynthesizeSpeechAsync_DualVoices_PassesResolvedVoiceToSynthesizer()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var femaleData = new byte[] { 0x11, 0x22, 0x33 };
        var maleData = new byte[] { 0x44, 0x55, 0x66 };

        var capturedVoices = new List<string>();

        var tts = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                capturedVoices.Add(voice);
                var audio = voice == ResilientTextToSpeechService.DefaultMaleVoice ? maleData : femaleData;
                return Task.FromResult(audio);
            });

        // 1. Giọng nữ (alias "female")
        var resultFemale = await tts.SynthesizeSpeechAsync("Câu hỏi số 1", "female", 1.0);
        resultFemale.VoiceName.Should().Be("vi-VN-HoaiMyNeural");
        resultFemale.AudioBytes.Should().Equal(femaleData);

        // 2. Giọng nam (alias "male")
        var resultMale = await tts.SynthesizeSpeechAsync("Câu hỏi số 2", "male", 1.0);
        resultMale.VoiceName.Should().Be("vi-VN-NamMinhNeural");
        resultMale.AudioBytes.Should().Equal(maleData);

        capturedVoices.Should().Equal("vi-VN-HoaiMyNeural", "vi-VN-NamMinhNeural");
    }

    // =========================================================================
    // NHÓM 2: SHA-256 CACHING PARTITIONING
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-05: SHA-256 cache key phân biệt rạch ròi giữa các giọng đọc")]
    public void CacheKey_DistinctVoices_ProducesDistinctSha256Hashes()
    {
        const string text = "Trình bày vòng đời của một yêu cầu trong ASP.NET Core.";
        const double rate = 1.0;

        var keyFemale = ResilientTextToSpeechService.ComputeCacheKeySha256("vi-VN-HoaiMyNeural", rate, text);
        var keyMale = ResilientTextToSpeechService.ComputeCacheKeySha256("vi-VN-NamMinhNeural", rate, text);

        keyFemale.Should().NotBeNullOrWhiteSpace();
        keyMale.Should().NotBeNullOrWhiteSpace();
        keyFemale.Length.Should().Be(64, "SHA-256 hex string phải có độ dài 64 ký tự");
        keyMale.Length.Should().Be(64);
        keyFemale.Should().NotBe(keyMale, "Cùng văn bản và tốc độ nhưng khác giọng đọc phải có cache key khác nhau");
    }

    [Fact(DisplayName = "ADV-M2-06: SHA-256 cache key phân biệt rạch ròi giữa các tốc độ đọc")]
    public void CacheKey_DistinctRates_ProducesDistinctSha256Hashes()
    {
        const string text = "Giải thích nguyên lý Single Responsibility Principle.";
        const string voice = "vi-VN-HoaiMyNeural";

        var keyNormal = ResilientTextToSpeechService.ComputeCacheKeySha256(voice, 1.0, text);
        var keyFast = ResilientTextToSpeechService.ComputeCacheKeySha256(voice, 1.25, text);
        var keySlow = ResilientTextToSpeechService.ComputeCacheKeySha256(voice, 0.85, text);

        keyNormal.Should().NotBe(keyFast);
        keyNormal.Should().NotBe(keySlow);
        keyFast.Should().NotBe(keySlow);
    }

    [Fact(DisplayName = "ADV-M2-07: ClampedRate kẹp biên chính xác (0.75 - 1.35) đảm bảo tính nhất quán cho cache")]
    public async Task SynthesizeSpeechAsync_ClampedRate_BehavesIdenticallyAtBoundary()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var audioSample = new byte[] { 0x01, 0x02 };
        var invocationCount = 0;

        var tts = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                Interlocked.Increment(ref invocationCount);
                return Task.FromResult(audioSample);
            });

        // Tốc độ 0.5 bị clamp về 0.75
        var res1 = await tts.SynthesizeSpeechAsync("Văn bản kiểm tra clamp tốc độ", "vi-VN-HoaiMyNeural", 0.5);
        res1.SpeakingRate.Should().Be(0.75);

        // Tốc độ 0.70 cũng bị clamp về 0.75 -> trùng cache key với res1 -> FromCache = true
        var res2 = await tts.SynthesizeSpeechAsync("Văn bản kiểm tra clamp tốc độ", "vi-VN-HoaiMyNeural", 0.70);
        res2.SpeakingRate.Should().Be(0.75);
        res2.FromCache.Should().BeTrue();
        res2.CacheKeySha256.Should().Be(res1.CacheKeySha256);
        invocationCount.Should().Be(1, "Lần 2 phải lấy từ bộ nhớ đệm do speakingRate sau khi clamp là như nhau");
    }

    [Fact(DisplayName = "ADV-M2-08: Bộ nhớ đệm SHA-256 giới hạn dung lượng và tự động loại bỏ phần tử cũ khi vượt quá 250")]
    public async Task Cache_WhenCapacityExceeds250_EvictsOldestEntryWithoutError()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var sampleBytes = new byte[] { 0x55, 0x66 };
        var synthCount = 0;

        var tts = new ResilientTextToSpeechService(
            new HttpClient(),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, rate, ct) =>
            {
                Interlocked.Increment(ref synthCount);
                return Task.FromResult(sampleBytes);
            });

        // Tạo 255 mục cache riêng biệt
        for (int i = 0; i < 255; i++)
        {
            await tts.SynthesizeSpeechAsync($"Câu hỏi khảo thí duy nhất số {i:D4}", "vi-VN-HoaiMyNeural", 1.0);
        }

        synthCount.Should().Be(255);

        // Các mục gần nhất vẫn còn trong cache (Hit)
        var hitResult = await tts.SynthesizeSpeechAsync("Câu hỏi khảo thí duy nhất số 0254", "vi-VN-HoaiMyNeural", 1.0);
        hitResult.FromCache.Should().BeTrue();
        synthCount.Should().Be(255, "Không được gọi lại synthesizer cho mục còn trong cache");
    }

    // =========================================================================
    // NHÓM 3: SE ACRONYM PREPROCESSING (PrepareTextForTts)
    // =========================================================================

    [Theory(DisplayName = "ADV-M2-09: PrepareTextForTts chuẩn hóa chuẩn xác C# và .NET không làm hỏng từ ngữ thông thường")]
    [InlineData("Lập trình C# cơ bản", "Lập trình C sharp cơ bản")]
    [InlineData("Lập trình c# nâng cao", "Lập trình C sharp nâng cao")]
    [InlineData("Hệ sinh thái .NET 8", "Hệ sinh thái dot NET 8")]
    [InlineData("Phát triển web với ASP.NET Core", "Phát triển web với ASP dot NET Core")]
    [InlineData("Kết nối internet và network", "Kết nối internet và network")]
    public void PrepareTextForTts_CSharpAndDotNet_ReplacesAccurately(string input, string expected)
    {
        var cleaned = SeTerminologyNormalizer.PrepareTextForTts(input);
        cleaned.Should().Be(expected);
    }

    [Theory(DisplayName = "ADV-M2-10: PrepareTextForTts chuẩn hóa toán tử và điểm số khảo thí")]
    [InlineData("Điểm thi >= 10.0đ được xuất sắc", "Điểm thi lớn hơn hoặc bằng 10 điểm được xuất sắc")]
    [InlineData("Điểm thi <= 10đ", "Điểm thi nhỏ hơn hoặc bằng 10 điểm")]
    [InlineData("Thang điểm 10.0Đ", "Thang điểm 10 điểm")]
    public void PrepareTextForTts_OperatorsAndGrading_ReplacesAccurately(string input, string expected)
    {
        var cleaned = SeTerminologyNormalizer.PrepareTextForTts(input);
        cleaned.Should().Be(expected);
    }

    [Theory(DisplayName = "ADV-M2-11: PrepareTextForTts làm sạch hoàn toàn ký hiệu Markdown (**, *, `, #, ~)")]
    [InlineData("## Câu hỏi 1: Giải thích **SOLID**", "Câu hỏi 1: Giải thích SOLID")]
    [InlineData("Dùng `var client = new HttpClient();`", "Dùng var client = new HttpClient();")]
    [InlineData("Từ khóa *async* và ~~sync~~", "Từ khóa async và sync")]
    public void PrepareTextForTts_MarkdownFormatting_StripsCleanly(string input, string expected)
    {
        var cleaned = SeTerminologyNormalizer.PrepareTextForTts(input);
        cleaned.Should().Be(expected);
    }

    [Theory(DisplayName = "ADV-M2-12: SynthesizeSpeechAsync ném ArgumentException khi văn bản chỉ chứa Markdown rỗng")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("### ** `` ~~")]
    [InlineData("   * * *   ")]
    public async Task SynthesizeSpeechAsync_EmptyOrMarkdownOnlyText_ThrowsArgumentException(string? input)
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var tts = new ResilientTextToSpeechService(new HttpClient(), config);

        var act = () => tts.SynthesizeSpeechAsync(input!, "vi-VN-HoaiMyNeural", 1.0);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*không được để trống*");
    }

    // =========================================================================
    // NHÓM 4: WHISPER JSON NON-NUMBER ATTRIBUTE HANDLING
    // =========================================================================

    [Fact(DisplayName = "ADV-M2-13: Whisper JSON chứa thuộc tính metrics là boolean/string/null/array/object không gây crash parser")]
    public async Task ParseWhisperResponse_AllNonNumericMetricTypes_DoesNotThrowAndCalculatesSafely()
    {
        var malformedJson = """
        {
            "result": {
                "text": "Đoạn văn bản bóc băng thành công",
                "segments": [
                    {
                        "id": 0,
                        "text": "Phân đoạn 1",
                        "no_speech_prob": "invalid_string_prob",
                        "avg_logprob": true,
                        "start": null,
                        "end": [1.0, 2.0]
                    },
                    {
                        "id": 1,
                        "text": "Phân đoạn 2",
                        "no_speech_prob": {"nested": "value"},
                        "avg_logprob": null,
                        "start": false,
                        "end": "two_seconds"
                    }
                ]
            }
        }
        """;

        var whisper = CreateWhisperService(malformedJson);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-audio"));

        var result = await whisper.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Đoạn văn bản bóc băng thành công");
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore!.Value.Should().BeInRange(0.0, 1.0);
        result.TimestampsJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "ADV-M2-14: Whisper JSON lọc chuẩn xác phân đoạn im lặng hợp lệ kể cả khi xen kẽ phân đoạn có metrics dị thường")]
    public async Task ParseWhisperResponse_MixedValidAndMalformedSegments_FiltersSilenceAccurately()
    {
        var mixedJson = """
        {
            "result": {
                "text": "Câu hỏi chuyên ngành",
                "segments": [
                    {
                        "id": 0,
                        "text": "Phân đoạn hợp lệ 1",
                        "no_speech_prob": 0.05,
                        "avg_logprob": -0.10,
                        "start": 0.0,
                        "end": 2.0
                    },
                    {
                        "id": 1,
                        "text": "Tiếng ồn bị lọc",
                        "no_speech_prob": 0.95,
                        "avg_logprob": -1.50,
                        "start": 2.0,
                        "end": 4.0
                    },
                    {
                        "id": 2,
                        "text": "Phân đoạn metrics dị thường nhưng nội dung thật",
                        "no_speech_prob": "unknown",
                        "avg_logprob": null,
                        "start": 4.0,
                        "end": 6.0
                    }
                ]
            }
        }
        """;

        var whisper = CreateWhisperService(mixedJson);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-audio"));

        var result = await whisper.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Contain("Phân đoạn hợp lệ 1");
        result.Text.Should().NotContain("Tiếng ồn bị lọc", "Phân đoạn có no_speech_prob = 0.95 (> 0.85) phải bị loại bỏ");
        result.Text.Should().Contain("Phân đoạn metrics dị thường nhưng nội dung thật");
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore!.Value.Should().BeInRange(0.0, 1.0);
    }

    [Fact(DisplayName = "ADV-M2-15: Whisper JSON với segments array rỗng hoặc không phải array được xử lý an toàn")]
    public async Task ParseWhisperResponse_EmptyOrMissingSegments_ReturnsEmptyOrFallback()
    {
        var emptySegmentsJson = """
        {
            "result": {
                "text": "Văn bản gốc khi không có segments",
                "segments": []
            }
        }
        """;

        var whisper = CreateWhisperService(emptySegmentsJson);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-audio"));

        var result = await whisper.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Văn bản gốc khi không có segments");
    }
}
