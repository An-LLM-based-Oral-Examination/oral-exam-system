using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using API.Controllers.v1;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;
using Xunit;

namespace OralExamination.UnitTests.Features.Controllers;

public class StorageControllerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly StorageController _controller;

    public StorageControllerTests()
    {
        _controller = new StorageController(_senderMock.Object);
    }

    private static byte[] CreateValidWebmPayload(string trailingMarker = "audio-payload")
    {
        var markerBytes = Encoding.UTF8.GetBytes(trailingMarker);
        var bytes = new byte[16 + markerBytes.Length];
        // WebM / EBML magic header: 1A 45 DF A3
        bytes[0] = 0x1A;
        bytes[1] = 0x45;
        bytes[2] = 0xDF;
        bytes[3] = 0xA3;
        markerBytes.CopyTo(bytes, 16);
        return bytes;
    }

    [Fact(DisplayName = "1. UploadAudio trả về HTTP 200 OK kèm Transcript khi tải lên audio hợp lệ")]
    public async Task UploadAudio_WithValidAudioFile_Returns200OkWithTranscript()
    {
        // Arrange
        var fileName = "answer.webm";
        var stream = new MemoryStream(CreateValidWebmPayload("sample audio content"));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/webm"
        };

        var expectedResponse = new UploadAudioPracticeResponse("Kiến trúc Clean Architecture gồm 4 tầng độc lập.");

        _senderMock
            .Setup(s => s.Send(It.IsAny<UploadAudioPracticeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadAudioPracticeResponse>.Success(expectedResponse));

        // Act
        var result = await _controller.UploadAudio(file);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact(DisplayName = "2. UploadAudio trả về HTTP 400 BadRequest khi file null hoặc 0 byte")]
    public async Task UploadAudio_WithNullOrEmptyFile_Returns400BadRequest()
    {
        // Act - Null file
        var nullResult = await _controller.UploadAudio(null);
        var badRequest1 = nullResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest1.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Act - Empty file
        using var emptyStream = new MemoryStream();
        IFormFile emptyFile = new FormFile(emptyStream, 0, 0, "file", "empty.webm");
        var emptyResult = await _controller.UploadAudio(emptyFile);
        var badRequest2 = emptyResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest2.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "3. UploadAudio trả về HTTP 400 BadRequest khi dung lượng file vượt quá 25MB")]
    public async Task UploadAudio_WithExceedingSizeFile_Returns400BadRequest()
    {
        // Arrange - Mock an oversized file (26MB) without allocating memory
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(26 * 1024 * 1024); // 26MB
        fileMock.Setup(f => f.FileName).Returns("huge.webm");
        fileMock.Setup(f => f.ContentType).Returns("audio/webm");

        // Act
        var result = await _controller.UploadAudio(fileMock.Object);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        badRequest.Value.Should().NotBeNull();
        badRequest.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().Contain("vượt quá giới hạn cho phép");
    }

    [Fact(DisplayName = "4. UploadAudio trả về HTTP 400 BadRequest khi Command xử lý thất bại")]
    public async Task UploadAudio_WhenCommandFails_Returns400BadRequestWithError()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidWebmPayload("sample audio content"));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "test.webm");

        _senderMock
            .Setup(s => s.Send(It.IsAny<UploadAudioPracticeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadAudioPracticeResponse>.Failure("Cloudflare Whisper timeout"));

        // Act
        var result = await _controller.UploadAudio(file);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        badRequest.Value.Should().NotBeNull();
        badRequest.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().Contain("Cloudflare Whisper timeout");
    }

    [Fact(DisplayName = "5. UploadAudio tự động nhận diện file từ Request.Form.Files khi trường form khác tên 'file'")]
    public async Task UploadAudio_WithAlternativeFormField_FallsBackToFormFiles()
    {
        // Arrange
        var stream = new MemoryStream(CreateValidWebmPayload("audio binary"));
        IFormFile formFile = new FormFile(stream, 0, stream.Length, "audioRecording", "recording.webm");

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "multipart/form-data";
        var formFileCollection = new FormFileCollection { formFile };
        httpContext.Request.Form = new FormCollection(new System.Collections.Generic.Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), formFileCollection);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var expectedResponse = new UploadAudioPracticeResponse("Nhận diện từ audioRecording thành công");
        _senderMock
            .Setup(s => s.Send(It.IsAny<UploadAudioPracticeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadAudioPracticeResponse>.Success(expectedResponse));

        // Act - Pass null to trigger fallback
        var result = await _controller.UploadAudio(null);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact(DisplayName = "6. UploadAudio trả về HTTP 415 UnsupportedMediaType ProblemDetails khi file không có magic-bytes âm thanh hợp lệ")]
    public async Task UploadAudio_WithInvalidMagicBytes_Returns415UnsupportedMediaTypeWithoutCallingStt()
    {
        // Arrange
        using var junkStream = new MemoryStream(Encoding.UTF8.GetBytes("<html>not-an-audio-file</html>"));
        IFormFile junkFile = new FormFile(junkStream, 0, junkStream.Length, "file", "fake.webm")
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/webm"
        };

        // Act
        var result = await _controller.UploadAudio(junkFile);

        // Assert
        var unsupported = result.Should().BeOfType<ObjectResult>().Subject;
        unsupported.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        var problem = unsupported.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status415UnsupportedMediaType);
        problem.Extensions["errorCode"].Should().Be("UNSUPPORTED_AUDIO_MEDIA_TYPE");
        _senderMock.Verify(
            s => s.Send(It.IsAny<UploadAudioPracticeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
