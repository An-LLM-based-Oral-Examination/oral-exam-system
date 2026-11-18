using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OralExamination.Application.Common.Interfaces;

namespace OralExamination.API.Middlewares;

public partial class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const int MaxCorrelationIdLength = 64;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCorrelationIdRegex();

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly IRuntimeMetricsTracker _metricsTracker;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger,
        IRuntimeMetricsTracker metricsTracker)
    {
        _next = next;
        _logger = logger;
        _metricsTracker = metricsTracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var rawCorrelationId = context.Request.Headers[CorrelationIdHeader].ToString();

        var correlationId = !string.IsNullOrEmpty(rawCorrelationId) && SafeCorrelationIdRegex().IsMatch(rawCorrelationId)
            ? rawCorrelationId
            : Guid.NewGuid().ToString("D");

        context.TraceIdentifier = correlationId;

        // Set response header eagerly and safeguard with OnStarting callback
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            }
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceIdentifier"] = correlationId
        }))
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                _metricsTracker.RecordRequest(context.Response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}
