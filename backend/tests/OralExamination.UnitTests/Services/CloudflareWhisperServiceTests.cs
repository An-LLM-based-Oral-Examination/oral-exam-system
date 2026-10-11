using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OralExamination.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using Xunit;

namespace OralExamination.UnitTests.Services;

public class CloudflareWhisperServiceTests
{
    private readonly IConfiguration _validConfig;

    public CloudflareWhisperServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Cloudflare:AccountId", "test-account-id-12345"},
            {"Cloudflare:ApiToken", "test-token-abcdef"}
        };

        _validConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact(DisplayName = "1. Thiếu thông tin xác thực Cloudflare ném InvalidOperationException")]
    public async Task TranscribeAudioAsync_WithMissingCredentials_ThrowsInvalidOperationException()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new CloudflareWhisperService(httpClient, emptyConfig);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("some-audio"));

        var act = async () => await service.TranscribeAudioAsync(stream, "test.webm", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cloudflare Credentials for Whisper are missing*");
    }

    [Fact(DisplayName = "2. AudioStream là null trả về chuỗi rỗng và không gửi HTTP request")]
    public async Task TranscribeAudioAsync_WithNullStream_ReturnsEmptyString()
    {
        var handlerCalled = false;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig);

        var result = await service.TranscribeAudioAsync(null!, "test.webm", CancellationToken.None);

        result.Should().BeEmpty();
        handlerCalled.Should().BeFalse("Không được gửi HTTP request khi stream là null");
    }

    [Fact(DisplayName = "3. AudioStream seekable có độ dài 0 byte trả về chuỗi rỗng không gửi HTTP request")]
    public async Task TranscribeAudioAsync_WithEmptySeekableStream_ReturnsEmptyString()
    {
        var handlerCalled = false;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig);
        using var emptyStream = new MemoryStream();

        var result = await service.TranscribeAudioAsync(emptyStream, "test.webm", CancellationToken.None);

        result.Should().BeEmpty();
        handlerCalled.Should().BeFalse("Không được gửi HTTP request khi stream seekable có độ dài 0");
    }

    [Fact(DisplayName = "4. AudioStream non-seekable rỗng trả về chuỗi rỗng không gửi HTTP request")]
    public async Task TranscribeAudioAsync_WithEmptyNonSeekableStream_ReturnsEmptyString()
    {
        var handlerCalled = false;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            handlerCalled = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig);
        using var emptyMs = new MemoryStream();
        using var nonSeekable = new Features.Practice.NonSeekableTestStream(emptyMs);

        var result = await service.TranscribeAudioAsync(nonSeekable, "test.webm", CancellationToken.None);

        result.Should().BeEmpty();
        handlerCalled.Should().BeFalse("Không được gửi HTTP request khi stream non-seekable rỗng");
    }

    [Fact(DisplayName = "5. AudioStream seekable chưa tua về 0 được tự động rewind và gửi dữ liệu thành công")]
    public async Task TranscribeAudioAsync_WithUnrewoundStream_RewindsAndSendsPayload()
    {
        var audioBytes = Encoding.UTF8.GetBytes("raw-audio-binary-data");
        using var stream = new MemoryStream(audioBytes);
        stream.Position = stream.Length; // Position at end

        byte[]? receivedBytes = null;
        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            if (req.Content != null)
            {
                receivedBytes = await req.Content.ReadAsByteArrayAsync(ct);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":{\"text\":\"Bóc băng từ stream đã tự động rewind\"}}", Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig);

        var result = await service.TranscribeAudioAsync(stream, "test.webm", CancellationToken.None);

        result.Should().Be("Bóc băng từ stream đã tự động rewind");
        receivedBytes.Should().NotBeNull();
        receivedBytes.Should().BeEquivalentTo(audioBytes);
    }

    [Fact(DisplayName = "6. Cloudflare trả về HTTP 200 phân tích đúng JSON result.text")]
    public async Task TranscribeAudioAsync_WithValidAudio_ParsesResultText()
    {
        var audioBytes = Encoding.UTF8.GetBytes("valid-audio");
        using var stream = new MemoryStream(audioBytes);

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            req.Headers.Authorization.Should().NotBeNull();
            req.Headers.Authorization!.Scheme.Should().Be("Bearer");
            req.Headers.Authorization!.Parameter.Should().Be("test-token-abcdef");

            var responseJson = "{\"result\":{\"text\":\"Nguyên lý Dependency Inversion trong Clean Architecture\"}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        });
        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig);

        var result = await service.TranscribeAudioAsync(stream, "speech.webm", CancellationToken.None);

        result.Should().Be("Nguyên lý Dependency Inversion trong Clean Architecture");
    }

    [Fact(DisplayName = "7. Cloudflare trả về lỗi HTTP 500 ném HttpRequestException chi tiết sau khi retry")]
    public async Task TranscribeAudioAsync_WhenCloudflareReturnsError_ThrowsException()
    {
        var audioBytes = Encoding.UTF8.GetBytes("valid-audio");
        using var stream = new MemoryStream(audioBytes);

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Cloudflare AI Internal Model Error", Encoding.UTF8, "text/plain")
            });
        });

        var fastPipeline = new Polly.ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(5),
                BackoffType = Polly.DelayBackoffType.Constant,
                ShouldHandle = new Polly.PredicateBuilder().Handle<HttpRequestException>()
            })
            .Build();

        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig, fastPipeline);

        var act = async () => await service.TranscribeAudioAsync(stream, "speech.webm", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Cloudflare Whisper STT failed: InternalServerError*");
    }

    [Fact(DisplayName = "8. Polly tự động retry khi gặp HttpRequestException tạm thời và thành công ở lần thử tiếp theo")]
    public async Task TranscribeAudioAsync_WithTransientNetworkFailure_RetriesAndSucceeds()
    {
        var audioBytes = Encoding.UTF8.GetBytes("valid-audio");
        using var stream = new MemoryStream(audioBytes);
        int callCount = 0;

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException("Temporary network glitch");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":{\"text\":\"Thành công sau retry\"}}", Encoding.UTF8, "application/json")
            });
        });

        // Fast retry pipeline for tests (10ms delay)
        var fastPipeline = new Polly.ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = Polly.DelayBackoffType.Constant,
                ShouldHandle = new Polly.PredicateBuilder().Handle<HttpRequestException>()
            })
            .Build();

        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig, fastPipeline);

        var result = await service.TranscribeAudioAsync(stream, "speech.webm", CancellationToken.None);

        result.Should().Be("Thành công sau retry");
        callCount.Should().Be(2, "Lần đầu ném HttpRequestException, lần thứ hai retry thành công");
    }

    [Fact(DisplayName = "9. Polly tự động retry khi Cloudflare trả về HTTP 503 Service Unavailable và thành công ở lần thử tiếp theo")]
    public async Task TranscribeAudioAsync_WhenCloudflareReturns503_RetriesAndSucceeds()
    {
        var audioBytes = Encoding.UTF8.GetBytes("valid-audio");
        using var stream = new MemoryStream(audioBytes);
        int callCount = 0;

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Service Unavailable temporarily", Encoding.UTF8, "text/plain")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":{\"text\":\"Khôi phục sau 503 thành công\"}}", Encoding.UTF8, "application/json")
            });
        });

        var fastPipeline = new Polly.ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = Polly.DelayBackoffType.Constant,
                ShouldHandle = new Polly.PredicateBuilder().Handle<HttpRequestException>()
            })
            .Build();

        var httpClient = new HttpClient(handler);
        var service = new CloudflareWhisperService(httpClient, _validConfig, fastPipeline);

        var result = await service.TranscribeAudioAsync(stream, "speech.webm", CancellationToken.None);

        result.Should().Be("Khôi phục sau 503 thành công");
        callCount.Should().Be(2, "Lần đầu nhận 503 ném HttpRequestException, Polly retry thành công ở lần 2");
    }

    [Fact(DisplayName = "10. Cloudflare trả về schema JSON thay thế hoặc result null được bóc tách an toàn không crash")]
    public async Task TranscribeAudioAsync_WithAlternativeOrNullResultJson_ParsesSafely()
    {
        var audioBytes = Encoding.UTF8.GetBytes("valid-audio");
        using var stream1 = new MemoryStream(audioBytes);
        using var stream2 = new MemoryStream(audioBytes);

        // Case 1: Direct root text property
        var handlerDirectText = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"Bóc băng từ schema text trực tiếp\"}", Encoding.UTF8, "application/json")
            }));

        var service1 = new CloudflareWhisperService(new HttpClient(handlerDirectText), _validConfig);
        var result1 = await service1.TranscribeAudioAsync(stream1, "speech.webm", CancellationToken.None);
        result1.Should().Be("Bóc băng từ schema text trực tiếp");

        // Case 2: Result is null
        var handlerNullResult = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"result\":null,\"success\":true}", Encoding.UTF8, "application/json")
            }));

        var service2 = new CloudflareWhisperService(new HttpClient(handlerNullResult), _validConfig);
        var result2 = await service2.TranscribeAudioAsync(stream2, "speech.webm", CancellationToken.None);
        result2.Should().BeEmpty("Khi result là null, service trả về chuỗi rỗng an toàn không văng exception");
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
    {
        _handlerFunc = handlerFunc;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handlerFunc(request, cancellationToken);
    }
}
