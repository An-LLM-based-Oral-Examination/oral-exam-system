namespace OralExamination.Infrastructure.Resilience;

/// <summary>Replays buffered provider requests, never a disposed or already-sent HttpRequestMessage.</summary>
public sealed class ExternalDependencyHttpHandler(
    ExternalDependencyResilience resilience,
    ExternalDependency dependency) : DelegatingHandler
{
    public static ExternalDependency ResolveDependency(ExternalDependency defaultDependency, Uri? requestUri)
    {
        var host = requestUri?.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return defaultDependency;
        }

        if (string.Equals(host, "translate.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalDependency.GoogleTts;
        }

        if (string.Equals(host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalDependency.Gemini;
        }

        if (defaultDependency != ExternalDependency.Whisper &&
            string.Equals(host, "api.cloudflare.com", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalDependency.CloudflareAi;
        }

        return defaultDependency;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        // Route strictly by target host so Google Translate TTS or Cloudflare Llama failures never trip the Gemini circuit.
        var selected = ResolveDependency(dependency, request.RequestUri);
        return await resilience.ExecuteAsync(selected, async ct =>
        {
            using var replay = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };
            foreach (var header in request.Headers) replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (body is not null)
            {
                replay.Content = new ByteArrayContent(body);
                foreach (var header in request.Content!.Headers)
                    replay.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            var response = await base.SendAsync(replay, ct);
            resilience.RecordHttpResponse(selected, response.StatusCode);
            try
            {
                if (ExternalDependencyResilience.IsTransientStatus(response.StatusCode))
                    throw new HttpRequestException($"Dependency {selected} returned HTTP {(int)response.StatusCode}.", null, response.StatusCode);
                // Include the response body in the timeout budget, not only connection/response headers.
                await response.Content.ReadAsByteArrayAsync(ct);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }, cancellationToken);
    }
}
