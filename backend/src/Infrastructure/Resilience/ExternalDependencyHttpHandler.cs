namespace OralExamination.Infrastructure.Resilience;

/// <summary>Replays buffered provider requests, never a disposed or already-sent HttpRequestMessage.</summary>
public sealed class ExternalDependencyHttpHandler(
    ExternalDependencyResilience resilience,
    ExternalDependency dependency) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        // Gemini services also call Cloudflare Llama as a fallback; keep its health isolated from Gemini/Whisper.
        var selected = dependency == ExternalDependency.Gemini &&
            string.Equals(request.RequestUri?.Host, "api.cloudflare.com", StringComparison.OrdinalIgnoreCase)
            ? ExternalDependency.CloudflareAi : dependency;
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
