using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Models;
using OralExamination.Infrastructure.Services;
using OralExamination.UnitTests.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OralExamination.UnitTests.Features.Adversarial;

/// <summary>
/// Bộ kiểm thử thực nghiệm đối kháng độc lập cho Milestone 2 (Empirical Adversarial Test Suite for M2):
/// Kiểm chứng độc lập các biên tham số tốc độ đọc (Speaking Rate) của Google Translate TTS
/// và chu kỳ kích hoạt, bỏ qua, phục hồi đóng băng (Cooldown Recovery) của Edge Neural TTS.
/// Được tạo và thực thi trực tiếp bởi challenger_m2_1.
/// </summary>
public class AdversarialMilestone2TtsRateAndCooldownChallengerTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static readonly byte[] DummyAudioBytes = [0xFF, 0xFB, 0x90, 0x64, 0x00, 0x01];

    // =========================================================================
    // NHÓM 1: KIỂM CHỨNG CÁC GIÁ TRỊ BIÊN TỐC ĐỘ ĐỌC (SPEAKING RATE BOUNDARIES)
    // =========================================================================

    [Theory(DisplayName = "ADV-TTS-01: Kiểm chứng thực nghiệm các ngưỡng biên speakingRate với URL ttsspeed của Google Translate")]
    [InlineData(0.50, "ttsspeed=0.5", 0.75)] // speakingRate 0.50 bị clamp thành 0.75, nhưng <= 0.85 nên ánh xạ ttsspeed=0.5
    [InlineData(0.85, "ttsspeed=0.5", 0.85)] // speakingRate 0.85 <= 0.85 nên ánh xạ ttsspeed=0.5
    [InlineData(0.86, "ttsspeed=0.86", 0.86)] // speakingRate 0.86 > 0.85 nên giữ nguyên 0.86
    [InlineData(1.00, "ttsspeed=1.0", 1.00)]  // speakingRate 1.00 trong khoảng sai số 0.03 nên ánh xạ ttsspeed=1.0
    public async Task SynthesizeSpeechAsync_SpeakingRateBoundaries_MapsToExpectedTtsSpeed(
        double speakingRate,
        string expectedTtsSpeedParam,
        double expectedClampedRate)
    {
        ResilientTextToSpeechService.ResetEdgeCooldown();

        var config = BuildConfig(new Dictionary<string, string?>());
        string? capturedUrl = null;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(DummyAudioBytes)
            });
        });

        // Cố tình ép Edge fail để request rơi vào Google Translate fallback
        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, r, ct) =>
                throw new HttpRequestException("Simulated Edge failure to trigger Google Translate TTS"),
            enableCooldownForCustom: false);

        var result = await ttsService.SynthesizeSpeechAsync(
            $"Kiểm thử đối kháng tốc độ đọc biên {speakingRate}",
            "vi-VN-HoaiMyNeural",
            speakingRate);

        result.Provider.Should().Be("GoogleNeuralTranslateTts");
        result.SpeakingRate.Should().Be(expectedClampedRate);
        result.UsedFallback.Should().BeTrue();
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain(expectedTtsSpeedParam);
    }

    [Fact(DisplayName = "ADV-TTS-02: Khảo sát thực nghiệm speakingRate = 1.50 — Xác định hành vi kẹp biên (Clamping Boundary) tại 1.35")]
    public async Task SynthesizeSpeechAsync_SpeakingRateAboveMax_InspectsClampingBehavior()
    {
        ResilientTextToSpeechService.ResetEdgeCooldown();

        var config = BuildConfig(new Dictionary<string, string?>());
        string? capturedUrl = null;

        var httpHandler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(DummyAudioBytes)
            });
        });

        var ttsService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, r, ct) =>
                throw new HttpRequestException("Simulated Edge failure to trigger Google Translate TTS"),
            enableCooldownForCustom: false);

        var result = await ttsService.SynthesizeSpeechAsync(
            "Kiểm thử đối kháng tốc độ đọc cực đại 1.50",
            "vi-VN-HoaiMyNeural",
            1.50);

        result.Provider.Should().Be("GoogleNeuralTranslateTts");
        capturedUrl.Should().NotBeNull();

        // Kiểm tra xem hệ thống clamp 1.50 về 1.35 (do Math.Clamp(rate, 0.75, 1.35)) hay truyền trực tiếp 1.5
        // Ghi nhận sự thật thực nghiệm:
        result.SpeakingRate.Should().Be(1.35, "ResilientTextToSpeechService giới hạn tốc độ tối đa là 1.35x theo thiết kế");
        capturedUrl.Should().Contain("ttsspeed=1.35");
    }

    // =========================================================================
    // NHÓM 2: KIỂM CHỨNG EDGE NEURAL COOLDOWN RECOVERY & THOÁT ĐÓNG BĂNG
    // =========================================================================

    [Fact(DisplayName = "ADV-TTS-03: Ngoại lệ Edge kích hoạt cooldown -> Lệnh kế tiếp bypass Edge -> ResetEdgeCooldown khôi phục ngay")]
    public async Task EdgeCooldown_ExceptionBypassesEdge_AndResetEdgeCooldownRestoresImmediately()
    {
        ResilientTextToSpeechService.ResetEdgeCooldown();
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();

        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Speech:EdgeCooldownSeconds"] = "120"
        });

        int edgeAttempts = 0;
        var httpHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(DummyAudioBytes)
            }));

        var service = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, r, ct) =>
            {
                Interlocked.Increment(ref edgeAttempts);
                throw new InvalidOperationException("WebSocket connection abruptly terminated");
            },
            enableCooldownForCustom: true);

        // Lần 1: Edge ném ngoại lệ -> kích hoạt cooldown
        var res1 = await service.SynthesizeSpeechAsync("Đoạn văn số 1 ném lỗi", "vi-VN-HoaiMyNeural", 1.0);
        res1.UsedFallback.Should().BeTrue();
        edgeAttempts.Should().Be(1);
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeTrue();
        ResilientTextToSpeechService.EdgeCooldownUntilUtc.Should().NotBeNull();

        // Lần 2 & 3: Trong thời gian cooldown -> bypass hoàn toàn Edge, không gọi lại primaryEdgeSynthesizer
        var res2 = await service.SynthesizeSpeechAsync("Đoạn văn số 2 trong cooldown", "vi-VN-HoaiMyNeural", 1.0);
        res2.UsedFallback.Should().BeTrue();
        edgeAttempts.Should().Be(1, "Edge synthesizer không được gọi lại khi đang trong thời gian cooldown");

        var res3 = await service.SynthesizeSpeechAsync("Đoạn văn số 3 trong cooldown", "vi-VN-HoaiMyNeural", 1.0);
        res3.UsedFallback.Should().BeTrue();
        edgeAttempts.Should().Be(1, "Edge synthesizer vẫn bị bỏ qua ở lần gọi thứ 3");

        // Gọi ResetEdgeCooldown() -> phục hồi ngay lập tức
        ResilientTextToSpeechService.ResetEdgeCooldown();
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();
        ResilientTextToSpeechService.EdgeCooldownUntilUtc.Should().BeNull();

        // Tạo service mới với Edge thành công
        var recoveringService = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, r, ct) =>
            {
                Interlocked.Increment(ref edgeAttempts);
                return Task.FromResult(DummyAudioBytes);
            },
            enableCooldownForCustom: true);

        var res4 = await recoveringService.SynthesizeSpeechAsync("Đoạn văn số 4 sau khi reset", "vi-VN-HoaiMyNeural", 1.0);
        res4.UsedFallback.Should().BeFalse();
        res4.Provider.Should().Be("EdgeNeuralTts");
        edgeAttempts.Should().Be(2, "Edge synthesizer được gọi lại ngay sau khi reset cooldown");
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();
    }

    [Fact(DisplayName = "ADV-TTS-04: Edge tổng hợp thành công tự động đặt lại ticks đóng băng về 0 sau khi hết hạn cooldown")]
    public async Task EdgeCooldown_SuccessfulSynthesis_ResetsCooldownTicksToZero()
    {
        // Giả lập trạng thái cooldown vừa hết hạn (hoặc được set duration rất ngắn)
        ResilientTextToSpeechService.SetEdgeCooldown(TimeSpan.FromMilliseconds(-100)); // Đã hết hạn trong quá khứ

        var config = BuildConfig(new Dictionary<string, string?>());
        var httpHandler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(DummyAudioBytes)
            }));

        var service = new ResilientTextToSpeechService(
            new HttpClient(httpHandler),
            config,
            logger: null,
            primaryEdgeSynthOverride: (text, voice, r, ct) => Task.FromResult(DummyAudioBytes),
            enableCooldownForCustom: true);

        var result = await service.SynthesizeSpeechAsync("Đoạn văn kiểm tra tự động reset ticks", "vi-VN-HoaiMyNeural", 1.0);

        result.UsedFallback.Should().BeFalse();
        result.Provider.Should().Be("EdgeNeuralTts");
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();
        ResilientTextToSpeechService.EdgeCooldownUntilUtc.Should().BeNull();
    }

    // =========================================================================
    // NHÓM 3: STRESS VÀ CONCURRENCY TESTING CHO COOLDOWN VÀ RATE
    // =========================================================================

    [Fact(DisplayName = "ADV-TTS-05: Stress concurrency 50 tác vụ đồng thời gọi ResetEdgeCooldown và SetEdgeCooldown")]
    public void EdgeCooldown_HighConcurrencyStress_ThreadSafeWithoutExceptions()
    {
        var exceptions = new ConcurrentBag<Exception>();
        var countdown = new CountdownEvent(50);

        for (int i = 0; i < 50; i++)
        {
            int index = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (index % 2 == 0)
                    {
                        ResilientTextToSpeechService.SetEdgeCooldown(TimeSpan.FromSeconds(index + 1));
                        _ = ResilientTextToSpeechService.IsEdgeInCooldown;
                        _ = ResilientTextToSpeechService.EdgeCooldownUntilUtc;
                    }
                    else
                    {
                        ResilientTextToSpeechService.ResetEdgeCooldown();
                        _ = ResilientTextToSpeechService.IsEdgeInCooldown;
                        _ = ResilientTextToSpeechService.EdgeCooldownUntilUtc;
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                finally
                {
                    countdown.Signal();
                }
            });
        }

        countdown.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("50 luồng concurrency phải hoàn thành trong 5 giây");
        exceptions.Should().BeEmpty("Không có bất kỳ ngoại lệ Race Condition nào xảy ra");

        // Dọn dẹp trạng thái
        ResilientTextToSpeechService.ResetEdgeCooldown();
        ResilientTextToSpeechService.IsEdgeInCooldown.Should().BeFalse();
    }
}
