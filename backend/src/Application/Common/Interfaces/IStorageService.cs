using System.IO;
using System.Threading.Tasks;

namespace OralExamination.Application.Common.Interfaces;

public interface IStorageService
{
    /// <summary>
    /// Uploads a file stream to Cloudflare R2 and returns the public URL.
    /// </summary>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
}
