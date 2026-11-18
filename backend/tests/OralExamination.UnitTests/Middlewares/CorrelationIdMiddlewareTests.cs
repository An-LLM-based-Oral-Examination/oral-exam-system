using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using OralExamination.API.Middlewares;
using OralExamination.Application.Common.Interfaces;
using Xunit;

namespace OralExamination.UnitTests.Middlewares;

public class CorrelationIdMiddlewareTests
{
    private readonly Mock<ILogger<CorrelationIdMiddleware>> _loggerMock = new();
    private readonly Mock<IRuntimeMetricsTracker> _metricsTrackerMock = new();

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_GeneratesNewGuidCorrelationId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        string? capturedTraceId = null;

        RequestDelegate next = ctx =>
        {
            capturedTraceId = ctx.TraceIdentifier;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.TraceIdentifier.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(context.TraceIdentifier, out var parsedGuid).Should().BeTrue();
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be(context.TraceIdentifier);
        capturedTraceId.Should().Be(context.TraceIdentifier);

        _metricsTrackerMock.Verify(m => m.RecordRequest(200, It.Is<double>(ms => ms >= 0)), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderPresent_PreservesProvidedCorrelationId()
    {
        // Arrange
        const string customCorrelationId = "custom-test-correlation-id-12345";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = customCorrelationId;
        string? capturedTraceId = null;

        RequestDelegate next = ctx =>
        {
            capturedTraceId = ctx.TraceIdentifier;
            ctx.Response.StatusCode = 204;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.TraceIdentifier.Should().Be(customCorrelationId);
        capturedTraceId.Should().Be(customCorrelationId);
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be(customCorrelationId);

        _metricsTrackerMock.Verify(m => m.RecordRequest(204, It.Is<double>(ms => ms >= 0)), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderIsWhitespace_GeneratesNewGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "   ";

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.TraceIdentifier.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(context.TraceIdentifier, out _).Should().BeTrue();
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be(context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderExceedsMaxLength_GeneratesNewGuid()
    {
        // Arrange
        var oversizedId = new string('A', CorrelationIdMiddleware.MaxCorrelationIdLength + 1);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = oversizedId;

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.TraceIdentifier.Should().NotBe(oversizedId);
        Guid.TryParse(context.TraceIdentifier, out var parsedGuid).Should().BeTrue();
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be(context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderIsAtMaxLength_PreservesProvidedCorrelationId()
    {
        // Arrange
        var exactMaxId = new string('B', CorrelationIdMiddleware.MaxCorrelationIdLength);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = exactMaxId;

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.TraceIdentifier.Should().Be(exactMaxId);
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader].ToString().Should().Be(exactMaxId);
    }

    [Fact]
    public async Task InvokeAsync_EstablishesLoggingScope_WithCorrelationIdAndTraceIdentifier()
    {
        // Arrange
        const string customCorrelationId = "test-scope-correlation-id";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = customCorrelationId;

        object? capturedScopeState = null;
        _loggerMock
            .Setup(l => l.BeginScope(It.IsAny<Dictionary<string, object>>()))
            .Callback<object>(state => capturedScopeState = state)
            .Returns(Mock.Of<IDisposable>());

        RequestDelegate next = ctx => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedScopeState.Should().NotBeNull();
        var scopeDict = capturedScopeState as Dictionary<string, object>;
        scopeDict.Should().NotBeNull();
        scopeDict.Should().ContainKey("CorrelationId").WhoseValue.Should().Be(customCorrelationId);
        scopeDict.Should().ContainKey("TraceIdentifier").WhoseValue.Should().Be(customCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_RecordsMetricsAndRethrows()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.StatusCode = 500;

        RequestDelegate next = ctx => throw new InvalidOperationException("Test unhandled exception");
        var middleware = new CorrelationIdMiddleware(next, _loggerMock.Object, _metricsTrackerMock.Object);

        // Act
        var act = () => middleware.InvokeAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test unhandled exception");
        _metricsTrackerMock.Verify(m => m.RecordRequest(500, It.Is<double>(ms => ms >= 0)), Times.Once);
    }
}
