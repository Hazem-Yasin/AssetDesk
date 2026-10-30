using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Enums;

namespace AssetDesk.Application.Abstractions;

public record AssetQuery(string? Search, AssetStatus? Status, AssetCategory? Category, int Page, int PageSize);

public interface IUserRepository
{
    Task<User?> GetAsync(int id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<List<User>> ListAsync(CancellationToken ct);
    void Add(User user);
}

public interface IAssetRepository
{
    Task<Asset?> GetAsync(int id, CancellationToken ct);
    Task<Asset?> GetDetailAsync(int id, CancellationToken ct);
    Task<bool> TagExistsAsync(string tag, int? exceptId, CancellationToken ct);
    Task<(List<Asset> Items, int Total)> SearchAsync(AssetQuery query, CancellationToken ct);
    Task<List<Asset>> ListForUserAsync(int userId, CancellationToken ct);
    Task<List<Assignment>> ListOverdueAsync(DateTime utcNow, CancellationToken ct);
    Task<Dictionary<AssetStatus, int>> CountByStatusAsync(CancellationToken ct);
    Task<Dictionary<AssetCategory, int>> CountByCategoryAsync(CancellationToken ct);
    Task<decimal> TotalActiveValueAsync(CancellationToken ct);
    void Add(Asset asset);
    void Remove(Asset asset);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Create(User user);
}

/// <summary>Object storage for attachments: local disk in development, Amazon S3 in AWS.</summary>
public interface IFileStore
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream> OpenAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
