using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using AssetDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AssetDesk.Infrastructure.Storage;

/// <summary>
/// Amazon S3 file store. Credentials come from the default AWS chain (IAM role on ECS/EC2/App Runner,
/// environment variables or a local profile) so no keys are ever stored in configuration.
/// </summary>
public class S3FileStore : IFileStore
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3FileStore(IOptions<StorageOptions> options)
    {
        var o = options.Value;
        _bucket = o.Bucket ?? throw new InvalidOperationException("Storage:Bucket is required for the S3 provider.");

        var config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(o.ServiceUrl))
        {
            config.ServiceURL = o.ServiceUrl;
            config.ForcePathStyle = true;
        }
        else if (!string.IsNullOrWhiteSpace(o.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(o.Region);
        }
        _s3 = new AmazonS3Client(config);
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        // S3 needs a seekable stream with a known length for a single PUT.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = buffer,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        }, ct);
    }

    public async Task<Stream> OpenAsync(string key, CancellationToken ct)
    {
        var response = await _s3.GetObjectAsync(_bucket, key, ct);
        return response.ResponseStream;
    }

    public Task DeleteAsync(string key, CancellationToken ct) => _s3.DeleteObjectAsync(_bucket, key, ct);
}
