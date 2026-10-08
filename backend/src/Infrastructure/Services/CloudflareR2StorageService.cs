using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using OralExamination.Application.Common.Interfaces;
using System.IO;
using System.Threading.Tasks;

namespace OralExamination.Infrastructure.Services;

public class CloudflareR2StorageService : IStorageService
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrlPrefix;

    public CloudflareR2StorageService(IConfiguration configuration)
    {
        var accessKey = configuration["Storage:AccessKey"];
        var secretKey = configuration["Storage:SecretKey"];
        var serviceUrl = configuration["Storage:ServiceUrl"];
        _bucketName = configuration["Storage:BucketName"] ?? "oral-exam-bucket";
        _publicUrlPrefix = configuration["Storage:PublicUrlPrefix"] ?? "https://pub-r2.oralexam.fpt.edu.vn";

        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true, // Bắt buộc cho R2
            AuthenticationRegion = "auto"
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var fileTransferUtility = new TransferUtility(_s3Client);
        
        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = fileName,
            BucketName = _bucketName,
            ContentType = contentType
        };

        await fileTransferUtility.UploadAsync(uploadRequest);

        // Trả về public URL
        return $"{_publicUrlPrefix}/{fileName}";
    }
}
