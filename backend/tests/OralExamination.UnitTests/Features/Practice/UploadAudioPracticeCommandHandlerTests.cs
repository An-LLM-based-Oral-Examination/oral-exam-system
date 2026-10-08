using FluentAssertions;
using Moq;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Features.Practice.Commands.UploadAudioPractice;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OralExamination.UnitTests.Features.Practice;

public class UploadAudioPracticeCommandHandlerTests
{
    private readonly Mock<ISpeechToTextService> _sttServiceMock;
    private readonly UploadAudioPracticeCommandHandler _handler;

    public UploadAudioPracticeCommandHandlerTests()
    {
        _sttServiceMock = new Mock<ISpeechToTextService>();
        _handler = new UploadAudioPracticeCommandHandler(_sttServiceMock.Object);
    }

    [Fact(DisplayName = "1. Stream audio hợp lệ chuyển thẳng sang Whisper STT và trả về Transcript ngay lập tức")]
    public async Task Handle_WithValidStream_StreamsDirectlyToWhisperAndReturnsTranscript()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("fake audio bytes data");
        using var stream = new MemoryStream(audioBytes);
        var expectedTranscript = "Giải thích nguyên lý SOLID trong C# .NET 8";

        _sttServiceMock
            .Setup(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "sample.webm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscript);

        var command = new UploadAudioPracticeCommand(stream, "sample.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Transcript.Should().Be(expectedTranscript);

        _sttServiceMock.Verify(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "sample.webm", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "2. Stream rỗng trả về Failure và không gọi Whisper STT")]
    public async Task Handle_WithEmptyStream_ReturnsFailure()
    {
        // Arrange
        using var emptyStream = new MemoryStream();
        var command = new UploadAudioPracticeCommand(emptyStream, "empty.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("File ghi âm không hợp lệ hoặc rỗng.");
        _sttServiceMock.Verify(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "3. Stream null trả về Failure")]
    public async Task Handle_WithNullStream_ReturnsFailure()
    {
        // Arrange
        var command = new UploadAudioPracticeCommand(null!, "null.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("File ghi âm không hợp lệ hoặc rỗng.");
    }

    [Fact(DisplayName = "4. Handler không còn phụ thuộc vào IStorageService (Cloudflare R2)")]
    public void Handler_Constructor_DoesNotDependOnStorageService()
    {
        // Verify constructor parameters
        var ctors = typeof(UploadAudioPracticeCommandHandler).GetConstructors();
        ctors.Should().HaveCount(1);
        
        var paramTypes = ctors[0].GetParameters().Select(p => p.ParameterType).ToList();
        paramTypes.Should().Contain(typeof(ISpeechToTextService));
        paramTypes.Should().NotContain(typeof(IStorageService), "MF-01/02 không được phụ thuộc hay upload lên R2");
    }

    [Fact(DisplayName = "5. Stream non-seekable rỗng trả về Failure")]
    public async Task Handle_WithNonSeekableEmptyStream_ReturnsFailure()
    {
        // Arrange
        using var emptyMs = new MemoryStream();
        using var nonSeekableStream = new NonSeekableTestStream(emptyMs);
        var command = new UploadAudioPracticeCommand(nonSeekableStream, "empty_nonseekable.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("File ghi âm không hợp lệ hoặc rỗng.");
        _sttServiceMock.Verify(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "6. Stream non-seekable có dữ liệu được buffer và gửi sang Whisper thành công")]
    public async Task Handle_WithNonSeekableValidStream_BuffersAndStreamsToWhisper()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("speech data in non-seekable stream");
        using var ms = new MemoryStream(audioBytes);
        using var nonSeekableStream = new NonSeekableTestStream(ms);
        var expectedTranscript = "Bóc băng thành công từ non-seekable stream";

        _sttServiceMock
            .Setup(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "nonseekable.webm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscript);

        var command = new UploadAudioPracticeCommand(nonSeekableStream, "nonseekable.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transcript.Should().Be(expectedTranscript);
        _sttServiceMock.Verify(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "nonseekable.webm", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "7. Stream seekable có Position > 0 được tự động tua về 0 trước khi gửi Whisper")]
    public async Task Handle_WithStreamPositionAtEnd_RewindsAndStreamsToWhisper()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("some sound data");
        using var stream = new MemoryStream(audioBytes);
        stream.Position = stream.Length; // Position at end
        var expectedTranscript = "Đã tua về đầu và bóc băng chính xác";

        _sttServiceMock
            .Setup(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "unrewound.webm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscript);

        var command = new UploadAudioPracticeCommand(stream, "unrewound.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transcript.Should().Be(expectedTranscript);
        stream.Position.Should().Be(0);
        _sttServiceMock.Verify(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "unrewound.webm", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "8. Whisper STT ném Exception được chuyển đổi thành Result.Failure an toàn")]
    public async Task Handle_WhenWhisperThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("sample sound");
        using var stream = new MemoryStream(audioBytes);

        _sttServiceMock
            .Setup(s => s.TranscribeAudioAsync(It.IsAny<Stream>(), "error.webm", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Cloudflare AI rate limit reached (HTTP 429)"));

        var command = new UploadAudioPracticeCommand(stream, "error.webm", "audio/webm");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Lỗi khi bóc băng âm thanh");
        result.Error.Should().Contain("HTTP 429");
    }
}

public class NonSeekableTestStream : Stream
{
    private readonly Stream _inner;
    public NonSeekableTestStream(Stream inner) => _inner = inner;
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
