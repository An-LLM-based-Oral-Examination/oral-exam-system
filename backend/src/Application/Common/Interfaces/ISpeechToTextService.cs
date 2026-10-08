using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OralExamination.Application.Common.Interfaces;

public interface ISpeechToTextService
{
    /// <summary>
    /// Bóc băng âm thanh thành văn bản sử dụng Cloudflare Whisper AI.
    /// </summary>
    Task<string> TranscribeAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken = default);
}
