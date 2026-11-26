using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;

namespace OralExamination.Application.Features.Speech;

internal static class SpeechUpstreamErrorClassifier
{
    public static string? ClassifyPrefix(Exception exception, CancellationToken callerCancellationToken)
    {
        return TryFindRootUpstreamStatusPrefix(exception, callerCancellationToken, out var prefix)
            ? prefix
            : null;
    }

    private static bool TryFindRootUpstreamStatusPrefix(
        Exception? exception,
        CancellationToken callerCancellationToken,
        out string prefix)
    {
        if (exception is null)
        {
            prefix = string.Empty;
            return false;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (var inner in aggregateException.InnerExceptions)
            {
                if (TryFindRootUpstreamStatusPrefix(inner, callerCancellationToken, out prefix))
                {
                    return true;
                }
            }
        }

        if (exception is TimeoutException)
        {
            prefix = "GATEWAY_TIMEOUT:";
            return true;
        }

        if (exception is OperationCanceledException && !callerCancellationToken.IsCancellationRequested)
        {
            prefix = "GATEWAY_TIMEOUT:";
            return true;
        }

        if (exception is HttpRequestException httpException)
        {
            prefix = httpException.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout
                ? "GATEWAY_TIMEOUT:"
                : "BAD_GATEWAY:";
            return true;
        }

        if (exception is WebSocketException)
        {
            prefix = "BAD_GATEWAY:";
            return true;
        }

        if (exception.InnerException is not null &&
            TryFindRootUpstreamStatusPrefix(exception.InnerException, callerCancellationToken, out prefix))
        {
            return true;
        }

        if (exception is InvalidOperationException && IsKnownUpstreamProviderFailure(exception.Message))
        {
            prefix = "BAD_GATEWAY:";
            return true;
        }

        prefix = string.Empty;
        return false;
    }

    private static bool IsKnownUpstreamProviderFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Gemini TTS", StringComparison.OrdinalIgnoreCase)
            || message.Contains("EdgeNeuralTts", StringComparison.OrdinalIgnoreCase)
            || message.Contains("GoogleNeuralTranslateTts", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CloudflareWhisper", StringComparison.OrdinalIgnoreCase)
            || message.Contains("GeminiMultimodalStt", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Cả Phương án 1", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Credentials", StringComparison.OrdinalIgnoreCase)
            || message.Contains("API key", StringComparison.OrdinalIgnoreCase);
    }
}
