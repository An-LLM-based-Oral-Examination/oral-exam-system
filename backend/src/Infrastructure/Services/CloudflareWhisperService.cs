using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Polly;
using Polly.Retry;

namespace OralExamination.Infrastructure.Services;

public class CloudflareWhisperService : ISpeechToTextService
{
    private readonly HttpClient _httpClient;
    private readonly string _accountId;
    private readonly string _apiToken;
    private readonly ResiliencePipeline _resiliencePipeline;

    public CloudflareWhisperService(
        HttpClient httpClient,
        IConfiguration configuration,
        ResiliencePipeline? resiliencePipeline = null)
    {
        _httpClient = httpClient;
        _accountId = configuration["Cloudflare:AccountId"] ?? string.Empty;
        _apiToken = configuration["Cloudflare:ApiToken"] ?? string.Empty;

        // Cấu hình Polly Resilience Pipeline: Retry 3 lần với Exponential Backoff (1s, 2s, 4s) khi gặp lỗi mạng/timeout
        _resiliencePipeline = resiliencePipeline ?? new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            })
            .Build();
    }

    public async Task<string> TranscribeAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_accountId) || string.IsNullOrEmpty(_apiToken))
        {
            throw new InvalidOperationException("Cloudflare Credentials for Whisper are missing.");
        }

        // Kiểm tra stream rỗng an toàn, tránh Cloudflare trả về 400 Bad Request
        if (audioStream == null)
        {
            return string.Empty;
        }

        if (audioStream.CanSeek)
        {
            if (audioStream.Length == 0)
            {
                return string.Empty;
            }

            if (audioStream.Position != 0)
            {
                audioStream.Position = 0;
            }
        }

        // Convert stream to byte array (Cloudflare AI requires binary payload)
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioBytes = memoryStream.ToArray();

        // Kiểm tra sau khi copy để chặn mọi stream rỗng (kể cả stream non-seekable)
        if (audioBytes.Length == 0)
        {
            return string.Empty;
        }

        var url = $"https://api.cloudflare.com/client/v4/accounts/{_accountId}/ai/run/@cf/openai/whisper";

        var responseString = await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

            var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = content;

            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Cloudflare Whisper STT failed: {response.StatusCode} - {body}", null, response.StatusCode);
            }

            return body;
        }, cancellationToken);

        using var document = JsonDocument.Parse(responseString);
        if (document.RootElement.TryGetProperty("result", out var resultElement) &&
            resultElement.ValueKind == JsonValueKind.Object &&
            resultElement.TryGetProperty("text", out var textElement))
        {
            return (textElement.GetString() ?? string.Empty).Trim();
        }

        if (document.RootElement.TryGetProperty("text", out var rootTextElement))
        {
            return (rootTextElement.GetString() ?? string.Empty).Trim();
        }

        return string.Empty;
    }
}
