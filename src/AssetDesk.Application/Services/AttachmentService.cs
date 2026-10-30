using AssetDesk.Application.Abstractions;
using AssetDesk.Application.Dtos;
using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Exceptions;

namespace AssetDesk.Application.Services;

public class AttachmentService(IAssetRepository assets, IFileStore files, IUnitOfWork uow, IClock clock)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedTypes =
        ["application/pdf", "image/png", "image/jpeg", "text/plain"];

    public async Task<AttachmentDto> UploadAsync(
        int assetId, string fileName, string contentType, long length, Stream content, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(assetId, ct) ?? throw new NotFoundException(nameof(Asset), assetId);
        asset.EnsureEditable();

        if (length <= 0) throw new DomainException("The file is empty.");
        if (length > MaxBytes) throw new DomainException($"Files are limited to {MaxBytes / 1024 / 1024} MB.");
        if (!AllowedTypes.Contains(contentType))
            throw new DomainException("Only PDF, PNG, JPEG and plain-text files are accepted.");

        // Never trust the client file name for the storage key.
        var key = $"assets/{assetId}/{Guid.NewGuid():N}";
        await files.SaveAsync(key, content, contentType, ct);

        var attachment = new Attachment
        {
            AssetId = assetId,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = length,
            StorageKey = key,
            UploadedAt = clock.UtcNow
        };
        asset.Attachments.Add(attachment);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            await files.DeleteAsync(key, CancellationToken.None); // do not orphan the object
            throw;
        }
        return Mapper.ToDto(attachment);
    }

    public async Task<(Stream Content, string FileName, string ContentType)> DownloadAsync(
        int assetId, int attachmentId, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(assetId, ct) ?? throw new NotFoundException(nameof(Asset), assetId);
        var attachment = asset.Attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException(nameof(Attachment), attachmentId);
        return (await files.OpenAsync(attachment.StorageKey, ct), attachment.FileName, attachment.ContentType);
    }

    public async Task DeleteAsync(int assetId, int attachmentId, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(assetId, ct) ?? throw new NotFoundException(nameof(Asset), assetId);
        asset.EnsureEditable();
        var attachment = asset.Attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new NotFoundException(nameof(Attachment), attachmentId);

        asset.Attachments.Remove(attachment);
        await uow.SaveChangesAsync(ct);
        await files.DeleteAsync(attachment.StorageKey, ct);
    }
}
