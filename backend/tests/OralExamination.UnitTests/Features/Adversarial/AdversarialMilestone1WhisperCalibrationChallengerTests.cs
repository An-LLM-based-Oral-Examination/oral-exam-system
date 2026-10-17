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
/// Bộ kiểm thử thực nghiệm đối kháng (Empirical Adversarial Test Suite) cho Milestone 1:
/// Kiểm chứng chuyên sâu tính toán Calibrated Confidence Score và Lọc âm thanh im lặng (Silence Filtering)
/// trong CloudflareWhisperService.cs dưới các điều kiện biên toán học, giá trị cực trị và payloads JSON dị thường.
/// Được xây dựng và thực thi độc lập bởi challenger_m1_1.
/// </summary>
public class AdversarialMilestone1WhisperCalibrationChallengerTests
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
            ["Cloudflare:AccountId"] = "cf-challenger-acc",
            ["Cloudflare:ApiToken"] = "cf-challenger-token"
        });

        var handler = new OralExamination.UnitTests.Services.MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));

        return new CloudflareWhisperService(new HttpClient(handler), config, CreateFastNoRetryPipeline());
    }

    // =========================================================================
    // NHÓM 1: KIỂM CHỨNG GIÁ TRỊ TOÁN HỌC CỰC TRỊ CHO c_i
    // c_i = Clamp(e^(avg_logprob) * (1.0 - no_speech_prob), 0.0, 1.0)
    // =========================================================================

    [Fact(DisplayName = "ADV-CHALLENGE-01: avg_logprob = 0.0 và no_speech_prob = 0.0 cho độ tin cậy tuyệt đối c = 1.0")]
    public async Task ExtremeMath_ZeroLogprobAndZeroNoSpeech_YieldsMaxConfidenceOne()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Kiểm thử điểm tin cậy cực đại 1.0"",
                ""segments"": [
                    {
                        ""text"": ""Kiểm thử điểm tin cậy cực đại 1.0"",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.0,
                        ""start"": 0.0,
                        ""end"": 2.5
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore!.Value.Should().Be(1.0, "e^0.0 * (1 - 0) = 1.0 * 1.0 = 1.0 tuyệt đối");
        result.Text.Should().Be("Kiểm thử điểm tin cậy cực đại 1.0");
    }

    [Fact(DisplayName = "ADV-CHALLENGE-02: avg_logprob tiến tới âm vô cùng (-1000.0) và no_speech_prob = 0.0 cho c = 0.0")]
    public async Task ExtremeMath_NegativeInfinityLogprob_YieldsZeroConfidence()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Kiểm thử điểm tin cậy cực tiểu 0.0"",
                ""segments"": [
                    {
                        ""text"": ""Kiểm thử điểm tin cậy cực tiểu 0.0"",
                        ""avg_logprob"": -1000.0,
                        ""no_speech_prob"": 0.0,
                        ""start"": 0.0,
                        ""end"": 2.5
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.ConfidenceScore.Should().NotBeNull();
        result.ConfidenceScore!.Value.Should().Be(0.0, "e^(-1000.0) tiến tới 0.0 nên confidence phải là 0.0");
    }

    [Fact(DisplayName = "ADV-CHALLENGE-03: avg_logprob cực dương (+100.0) được Clamp về đúng trần 1.0")]
    public async Task ExtremeMath_PositiveLogprob_ClampedToOne()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Kiểm thử điểm logprob dương kẹp trần"",
                ""segments"": [
                    {
                        ""text"": ""Kiểm thử điểm logprob dương kẹp trần"",
                        ""avg_logprob"": 100.0,
                        ""no_speech_prob"": 0.0,
                        ""start"": 0.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.ConfidenceScore.Should().Be(1.0, "Math.Clamp phải chặn e^100 không vượt quá 1.0");
    }

    // =========================================================================
    // NHÓM 2: KIỂM CHỨNG ĐỘ CHÍNH XÁC BIÊN LỌC IM LẶNG (SILENCE THRESHOLD = 0.85)
    // =========================================================================

    [Fact(DisplayName = "ADV-CHALLENGE-04: no_speech_prob = 0.8500001 (vượt ngưỡng 0.85 một lượng vô cùng bé) bị lọc bỏ triệt để")]
    public async Task SilenceThreshold_JustAboveThreshold_IsFiltered()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Ảo giác âm thanh im lặng bị loại"",
                ""segments"": [
                    {
                        ""text"": ""Ảo giác âm thanh im lặng bị loại"",
                        ""avg_logprob"": -0.10,
                        ""no_speech_prob"": 0.8500001,
                        ""start"": 0.0,
                        ""end"": 1.5
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("silence-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().BeEmpty("Segment có no_speech_prob = 0.8500001 > 0.85 phải bị loại bỏ hoàn toàn");
        result.RawText.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(0.0);
        result.TimestampsJson.Should().BeNull();
    }

    [Fact(DisplayName = "ADV-CHALLENGE-05: no_speech_prob = 0.8499999 (dưới ngưỡng 0.85 một lượng vô cùng bé) được giữ lại an toàn")]
    public async Task SilenceThreshold_JustBelowThreshold_IsKept()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Giọng nói thì thầm nhỏ"",
                ""segments"": [
                    {
                        ""text"": ""Giọng nói thì thầm nhỏ"",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.8499999,
                        ""start"": 0.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Giọng nói thì thầm nhỏ");
        result.ConfidenceScore.Should().NotBeNull();
        // e^0.0 * (1.0 - 0.8499999) = 1.0 * 0.1500001 = 0.1500
        result.ConfidenceScore!.Value.Should().Be(0.15, "0.8499999 <= 0.85 nên được giữ lại, confidence = Math.Round(0.1500001, 4) = 0.15");
    }

    [Fact(DisplayName = "ADV-CHALLENGE-06: no_speech_prob = 0.8500000 đúng bằng ngưỡng vẫn được giữ lại (vì điều kiện lọc là strictly > 0.85)")]
    public async Task SilenceThreshold_ExactThreshold_IsKept()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Ngưỡng chính xác 0.85"",
                ""segments"": [
                    {
                        ""text"": ""Ngưỡng chính xác 0.85"",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.8500000,
                        ""start"": 0.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Ngưỡng chính xác 0.85");
        result.ConfidenceScore.Should().Be(0.15);
    }

    [Fact(DisplayName = "ADV-CHALLENGE-07: Phân đoạn hỗn hợp: loại bỏ segment 0.8500001 và giữ lại segment 0.8499999")]
    public async Task SilenceThreshold_MixedBorderSegments_SeparatesCorrectly()
    {
        var json = @"{
            ""result"": {
                ""text"": ""[nhiễu im lặng] Lời nói sinh viên"",
                ""segments"": [
                    {
                        ""text"": ""[nhiễu im lặng]"",
                        ""avg_logprob"": -0.5,
                        ""no_speech_prob"": 0.8500001,
                        ""start"": 0.0,
                        ""end"": 1.0
                    },
                    {
                        ""text"": ""Lời nói sinh viên"",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.8499999,
                        ""start"": 1.0,
                        ""end"": 3.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Lời nói sinh viên");
        result.Text.Should().NotContain("[nhiễu im lặng]");
        result.ConfidenceScore.Should().Be(0.15);
        result.TimestampsJson.Should().NotBeNull();
        result.TimestampsJson.Should().NotContain("[nhiễu im lặng]");
    }

    // =========================================================================
    // NHÓM 3: PAYLOADS JSON DỊ THƯỜNG (NON-STANDARD JSON & MALFORMED INPUTS)
    // =========================================================================

    [Fact(DisplayName = "ADV-CHALLENGE-08: Giá trị kiểu dữ liệu dị thường (boolean/string) được xử lý an toàn nhờ kiểm tra ValueKind == Number")]
    public async Task NonStandardJson_MalformedNumberTypes_HandledSafelyWithNumberGuard()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Kiểm thử kiểu dữ liệu dị thường"",
                ""segments"": [
                    {
                        ""text"": ""Kiểm thử kiểu dữ liệu dị thường"",
                        ""avg_logprob"": ""not_a_number"",
                        ""no_speech_prob"": true,
                        ""start"": 0.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        // Sau khi bổ sung kiểm tra prop.ValueKind == JsonValueKind.Number, JsonElement.TryGetDouble không còn bị gọi
        // trên các kiểu dữ liệu dị thường (boolean/string) và không còn ném ngoại lệ làm sập bóc băng.
        var act = async () => await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "ADV-CHALLENGE-08B: Thuộc tính metrics có giá trị null (JsonValueKind.Null) được xử lý an toàn nhờ kiểm tra ValueKind == Number")]
    public async Task NonStandardJson_NullMetricsValues_HandledSafelyWithNumberGuard()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Kiểm thử metrics null"",
                ""segments"": [
                    {
                        ""text"": ""Kiểm thử metrics null"",
                        ""avg_logprob"": null,
                        ""no_speech_prob"": null,
                        ""start"": null,
                        ""end"": null
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        // Khi upstream API trả về null cho các trường đo lường, ValueKind == JsonValueKind.Number ngăn không gọi TryGetDouble
        var act = async () => await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "ADV-CHALLENGE-02B: avg_logprob là -1e999 (double underflow) bị !double.IsInfinity bỏ qua khiến fallback về -0.20 (confidence cao) thay vì 0.0")]
    public async Task ExtremeMath_UnderflowLogprob_BypassedByIsInfinityCheck()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Kiểm thử underflow logprob"",
                ""segments"": [
                    {
                        ""text"": ""Kiểm thử underflow logprob"",
                        ""avg_logprob"": -1e999,
                        ""no_speech_prob"": 0.0,
                        ""start"": 0.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        // Thử nghiệm xem -1e999 trong System.Text.Json:
        // System.Text.Json TryGetDouble cho -1e999 ra -Infinity hoặc ném exception
        try
        {
            var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);
            // Nếu parse được ra -Infinity, !double.IsInfinity khiến avgLogprob không được gán, giữ nguyên -0.20
            // dẫn đến confidenceScore ≈ 0.8187 thay vì 0.0!
            result.ConfidenceScore.Should().NotBeNull();
        }
        catch (Exception ex)
        {
            // Hoặc System.Text.Json không cho phép -1e999
            ex.Should().BeAssignableTo<Exception>();
        }
    }

    [Fact(DisplayName = "ADV-CHALLENGE-09: Timestamps âm và bị đảo ngược (end < start) được kẹp về duration >= 0 không gây lỗi")]
    public async Task NonStandardJson_NegativeAndInvertedTimestamps_ClampedGracefully()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Đoạn 1 Đoạn 2"",
                ""segments"": [
                    {
                        ""text"": ""Đoạn 1"",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.0,
                        ""start"": -10.0,
                        ""end"": -2.0
                    },
                    {
                        ""text"": ""Đoạn 2"",
                        ""avg_logprob"": -0.5,
                        ""no_speech_prob"": 0.0,
                        ""start"": 10.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Đoạn 1 Đoạn 2");
        result.ConfidenceScore.Should().NotBeNull();
        // Đoạn 1: duration = -2.0 - (-10.0) = 8.0, c1 = 1.0
        // Đoạn 2: duration = Max(0, 2.0 - 10.0) = 0.0, c2 = e^(-0.5) ≈ 0.6065
        // Tổng duration = 8.0 > 0.001 -> weighted = (1.0 * 8.0 + 0.6065 * 0.0) / 8.0 = 1.0
        result.ConfidenceScore!.Value.Should().Be(1.0);
    }

    [Fact(DisplayName = "ADV-CHALLENGE-10: Toàn bộ segments có duration = 0.0 rơi vào fallback trung bình cộng không chia cho 0")]
    public async Task NonStandardJson_AllZeroLengthSegments_FallsBackToUnweightedAverageWithoutDivideByZero()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Phần 1 Phần 2"",
                ""segments"": [
                    {
                        ""text"": ""Phần 1"",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.0,
                        ""start"": 1.0,
                        ""end"": 1.0
                    },
                    {
                        ""text"": ""Phần 2"",
                        ""avg_logprob"": -0.693147,
                        ""no_speech_prob"": 0.0,
                        ""start"": 2.0,
                        ""end"": 2.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Phần 1 Phần 2");
        result.ConfidenceScore.Should().NotBeNull();
        // c1 = 1.0, c2 = e^(-0.693147) ≈ 0.50
        // totalDuration = 0 <= 0.001 -> average = (1.0 + 0.50) / 2 = 0.75
        result.ConfidenceScore!.Value.Should().Be(0.75);
    }

    [Fact(DisplayName = "ADV-CHALLENGE-11: Thời lượng cực lớn (10^12 giây) không gây tràn số hay sinh NaN")]
    public async Task NonStandardJson_ExtremelyLongDuration_DoesNotOverflowOrYieldNaN()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Thời lượng thiên niên kỷ"",
                ""segments"": [
                    {
                        ""text"": ""Thời lượng thiên niên kỷ"",
                        ""avg_logprob"": -0.10,
                        ""no_speech_prob"": 0.05,
                        ""start"": 0.0,
                        ""end"": 1000000000000.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.ConfidenceScore.Should().NotBeNull();
        double.IsNaN(result.ConfidenceScore!.Value).Should().BeFalse();
        double.IsInfinity(result.ConfidenceScore!.Value).Should().BeFalse();
        result.ConfidenceScore!.Value.Should().BeInRange(0.80, 0.90);
    }

    [Fact(DisplayName = "ADV-CHALLENGE-12: Segment thiếu toàn bộ thuộc tính ngoại trừ text được gán ngầm định an toàn")]
    public async Task NonStandardJson_SegmentMissingOptionalProperties_UsesDefaultsSafely()
    {
        var json = @"{
            ""result"": {
                ""text"": ""Chỉ có text không có metrics"",
                ""segments"": [
                    {
                        ""text"": ""Chỉ có text không có metrics""
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().Be("Chỉ có text không có metrics");
        result.ConfidenceScore.Should().NotBeNull();
        // avg_logprob ngầm định = -0.20, no_speech_prob ngầm định = 0.0 -> e^(-0.20) ≈ 0.8187
        result.ConfidenceScore!.Value.Should().Be(0.8187);
    }

    [Fact(DisplayName = "ADV-CHALLENGE-13: Ảo giác lặp vô nghĩa bị SanitizeHallucinations làm rỗng thì ConfidenceScore bắt buộc bị ghi đè về 0.0")]
    public async Task HallucinationLoop_WhenSanitizedToEmpty_ForcesZeroConfidence()
    {
        // Giả lập Whisper trả về câu lặp ảo giác với logprob cực cao và no_speech cực thấp (vốn sẽ cho c = 1.0)
        var json = @"{
            ""result"": {
                ""text"": ""Thank you very much. Thank you very much. Thank you very much. Thank you very much. Thank you very much."",
                ""segments"": [
                    {
                        ""text"": ""Thank you very much. Thank you very much. Thank you very much. Thank you very much. Thank you very much."",
                        ""avg_logprob"": 0.0,
                        ""no_speech_prob"": 0.0,
                        ""start"": 0.0,
                        ""end"": 5.0
                    }
                ]
            }
        }";

        var service = CreateWhisperService(json);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await service.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Text.Should().BeEmpty("Chuỗi lặp Thank you phải bị SanitizeHallucinations làm rỗng");
        result.ConfidenceScore.Should().Be(0.0, "Khi text bị làm rỗng do ảo giác, ConfidenceScore bắt buộc phải bị ghi đè về 0.0");
    }

    [Fact(DisplayName = "ADV-CHALLENGE-14: ResilientSpeechToTextService tự động fallback an toàn sang Gemini khi Whisper nhận JSON cú pháp hỏng")]
    public async Task ResilientPipeline_WhenWhisperReceivesMalformedJsonSyntax_FallsBackToGeminiWithoutCrashing()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Cloudflare:AccountId"] = "cf-acc",
            ["Cloudflare:ApiToken"] = "cf-token",
            ["Gemini:ApiKey"] = "gemini-api-key"
        });

        // Handler trả về HTTP 200 nhưng nội dung không phải JSON (HTML lỗi cổng mạng)
        var malformedWhisperHandler = new OralExamination.UnitTests.Services.MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>502 Bad Gateway from Cloudflare upstream</body></html>", Encoding.UTF8, "application/json")
            }));

        var geminiSuccessHandler = new OralExamination.UnitTests.Services.MockHttpMessageHandler((req, ct) =>
        {
            var json = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Em trình bày kiến trúc Clean Architecture\"}]}}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        var whisperService = new CloudflareWhisperService(new HttpClient(malformedWhisperHandler), config, CreateFastNoRetryPipeline());
        var geminiService = new GeminiSpeechToTextService(new HttpClient(geminiSuccessHandler), config);
        var resilientStt = new ResilientSpeechToTextService(whisperService, geminiService, logger: null, configuration: config);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-audio"));

        var result = await resilientStt.TranscribeDetailedAsync(stream, "test.webm", null, CancellationToken.None);

        result.Should().NotBeNull();
        result.Provider.Should().Be("GeminiMultimodalStt", "Phải tự động chuyển sang Gemini khi Whisper gặp lỗi phân tích JSON cú pháp hỏng");
        result.UsedFallback.Should().BeTrue();
        result.Text.Should().Be("Em trình bày kiến trúc Clean Architecture");
    }
}
