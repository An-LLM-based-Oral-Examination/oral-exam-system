using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Services;

/// <summary>
/// Unit test suite kiểm chứng tính năng cấu hình động mô hình AI (Gemini Dynamic Model Selection)
/// và cơ chế Fallback ưu tiên tốc độ cao cho GeminiAiGradingService và GeminiQuestionGenerationService (Milestone M3):
/// - Tự động phân giải Gemini:Model / Gemini__Model / GEMINI_MODEL để xây dựng URL generateContent chính xác.
/// - Fallback về mặc định gemini-flash-lite-latest khi không có cấu hình.
/// - Ưu tiên tuyệt đối cho Gemini:ApiUrl / Gemini__ApiUrl khi được cung cấp tường minh (Backward Compatibility).
/// - Danh sách FallbackApiUrls đưa gemini-flash-lite-latest và gemini-3.1-flash-lite lên 2 vị trí ưu tiên cao nhất (index 0 và index 1).
/// - Cơ chế lọc và xoay vòng candidate API keys loại trừ triệt để placeholder keys.
/// </summary>
public class GeminiModelDynamicConfigTests
{
    private static GeminiAiGradingService CreateService(Dictionary<string, string?> inMemorySettings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var httpClient = new HttpClient();
        return new GeminiAiGradingService(httpClient, configuration, NullLogger<GeminiAiGradingService>.Instance);
    }

    private static string GetInternalApiUrl(GeminiAiGradingService service)
    {
        var field = typeof(GeminiAiGradingService).GetField("_apiUrl", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull("Field _apiUrl phải tồn tại trên GeminiAiGradingService");
        return (string)field!.GetValue(service)!;
    }

    private static IReadOnlyList<string> GetInternalCandidateKeys(GeminiAiGradingService service)
    {
        var field = typeof(GeminiAiGradingService).GetField("_candidateApiKeys", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull("Field _candidateApiKeys phải tồn tại trên GeminiAiGradingService");
        return (IReadOnlyList<string>)field!.GetValue(service)!;
    }

    private static string[] GetStaticFallbackApiUrls()
    {
        var field = typeof(GeminiAiGradingService).GetField("FallbackApiUrls", BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("Static field FallbackApiUrls phải tồn tại trên GeminiAiGradingService");
        return (string[])field!.GetValue(null)!;
    }

    [Fact(DisplayName = "GeminiModelConfig: Khi không cấu hình model, mặc định URL sử dụng gemini-flash-lite-latest")]
    public void Constructor_DefaultModel_WhenNoConfigProvided_ShouldConstructUrlWithFlashLiteLatest()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "fake-key"
        };

        // Act
        var service = CreateService(settings);
        var apiUrl = GetInternalApiUrl(service);

        // Assert
        apiUrl.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent");
    }

    [Fact(DisplayName = "GeminiModelConfig: Cấu hình Gemini:Model tùy chỉnh tạo đúng URL với model chỉ định")]
    public void Constructor_GeminiModelConfig_ShouldConstructUrlWithSpecifiedModel()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "fake-key",
            ["Gemini:Model"] = "gemini-3.1-flash-lite"
        };

        // Act
        var service = CreateService(settings);
        var apiUrl = GetInternalApiUrl(service);

        // Assert
        apiUrl.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent");
    }

    [Fact(DisplayName = "GeminiModelConfig: Hỗ trợ cấu hình dạng biến môi trường Docker Gemini__Model")]
    public void Constructor_GeminiDoubleUnderscoreModel_ShouldConstructUrlWithSpecifiedModel()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "docker-env-key",
            ["Gemini__Model"] = "gemini-flash-lite-latest"
        };

        // Act
        var service = CreateService(settings);
        var apiUrl = GetInternalApiUrl(service);

        // Assert
        apiUrl.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent");
    }

    [Fact(DisplayName = "GeminiModelConfig: Chuỗi model rỗng hoặc whitespace tự động fallback về gemini-flash-lite-latest")]
    public void Constructor_WhitespaceModel_ShouldFallbackToDefault()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "fake-key",
            ["Gemini:Model"] = "    "
        };

        // Act
        var service = CreateService(settings);
        var apiUrl = GetInternalApiUrl(service);

        // Assert
        apiUrl.Should().Be("https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent");
    }

    [Fact(DisplayName = "GeminiModelConfig: Cấu hình tường minh Gemini:ApiUrl được ưu tiên hơn Gemini:Model (Bảo toàn tương thích)")]
    public void Constructor_ExplicitApiUrl_ShouldTakePrecedenceOverModelConfig()
    {
        // Arrange
        var customEndpoint = "https://internal-gateway.fpt.edu.vn/v1beta/models/custom-model:generateContent";
        var settings = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "fake-key",
            ["Gemini:Model"] = "gemini-3.1-flash-lite", // Bị ghi đè bởi ApiUrl
            ["Gemini:ApiUrl"] = customEndpoint
        };

        // Act
        var service = CreateService(settings);
        var apiUrl = GetInternalApiUrl(service);

        // Assert
        apiUrl.Should().Be(customEndpoint);
    }

    [Fact(DisplayName = "GeminiModelConfig: Cấu hình tường minh dạng Docker Gemini__ApiUrl được ưu tiên hơn")]
    public void Constructor_ExplicitDoubleUnderscoreApiUrl_ShouldTakePrecedence()
    {
        // Arrange
        var customEndpoint = "https://production-proxy.oralexam.io.vn/v1beta/models/gemini-flash-lite:generateContent";
        var settings = new Dictionary<string, string?>
        {
            ["Gemini__ApiKey"] = "fake-key",
            ["Gemini__Model"] = "gemini-2.5-flash",
            ["Gemini__ApiUrl"] = customEndpoint
        };

        // Act
        var service = CreateService(settings);
        var apiUrl = GetInternalApiUrl(service);

        // Assert
        apiUrl.Should().Be(customEndpoint);
    }

    [Fact(DisplayName = "GeminiModelConfig: Mảng FallbackApiUrls đưa gemini-flash-lite-latest và gemini-3.1-flash-lite lên đầu tiên")]
    public void FallbackApiUrls_Should_Place_FlashLiteModels_At_Top()
    {
        // Act
        var fallbacks = GetStaticFallbackApiUrls();

        // Assert
        fallbacks.Should().NotBeNullOrEmpty();
        fallbacks.Length.Should().BeGreaterOrEqualTo(6);

        // Vị trí 0: gemini-flash-lite-latest
        fallbacks[0].Should().Be(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent",
            "Mô hình flash-lite mới nhất phải ở vị trí ưu tiên số 1");

        // Vị trí 1: gemini-3.1-flash-lite
        fallbacks[1].Should().Be(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent",
            "Mô hình 3.1 flash lite phải ở vị trí ưu tiên số 2");

        // Các mô hình dự phòng tiếp theo
        fallbacks.Should().Contain("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");
        fallbacks.Should().Contain("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent");
        fallbacks.Should().Contain("https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent");
        fallbacks.Should().Contain("https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent");
    }

    [Fact(DisplayName = "GeminiModelConfig: Danh sách candidate API keys loại trừ placeholder và lọc trùng an toàn")]
    public void CandidateApiKeys_WithMultiplePooledKeys_ShouldExcludePlaceholders()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "key-alpha",
            ["Gemini:FallbackApiKey"] = "key-beta",
            ["Gemini:ApiKeys"] = "key-gamma;YourActualApiKeyHere;key-alpha;PlaceholderValue;key-delta"
        };

        // Act
        var service = CreateService(settings);
        var candidateKeys = GetInternalCandidateKeys(service);

        // Assert
        candidateKeys.Should().NotBeNull();
        candidateKeys.Should().Contain("key-alpha");
        candidateKeys.Should().Contain("key-beta");
        candidateKeys.Should().Contain("key-gamma");
        candidateKeys.Should().Contain("key-delta");

        // Placeholder phải bị loại bỏ 100%
        candidateKeys.Should().NotContain(k => k.Contains("YourActual", StringComparison.OrdinalIgnoreCase));
        candidateKeys.Should().NotContain(k => k.Contains("Placeholder", StringComparison.OrdinalIgnoreCase));

        // Không bị trùng lặp key-alpha
        candidateKeys.Should().OnlyHaveUniqueItems();
    }
}
