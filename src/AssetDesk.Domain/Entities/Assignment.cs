namespace AssetDesk.Domain.Entities;

public class Assignment
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime CheckedOutAt { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? Notes { get; set; }

    public bool IsActive => ReturnedAt is null;
    public bool IsOverdue(DateTime utcNow) => IsActive && DueAt < utcNow;
}

public class Attachment
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    /// <summary>Object key in the file store (S3 key or relative local path).</summary>
    public string StorageKey { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
