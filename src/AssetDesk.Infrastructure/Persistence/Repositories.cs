using AssetDesk.Application.Abstractions;
using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssetDesk.Infrastructure.Persistence;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetAsync(int id, CancellationToken ct) => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<List<User>> ListAsync(CancellationToken ct) =>
        db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(ct);

    public void Add(User user) => db.Users.Add(user);
}

public class AssetRepository(AppDbContext db) : IAssetRepository
{
    private IQueryable<Asset> WithCurrent => db.Assets.Include(a => a.Assignments.Where(x => x.ReturnedAt == null)).ThenInclude(x => x.User);

    public Task<Asset?> GetAsync(int id, CancellationToken ct) => db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Asset?> GetDetailAsync(int id, CancellationToken ct) =>
        db.Assets
            .Include(a => a.Assignments).ThenInclude(x => x.User)
            .Include(a => a.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> TagExistsAsync(string tag, int? exceptId, CancellationToken ct) =>
        db.Assets.AnyAsync(a => a.Tag == tag && (exceptId == null || a.Id != exceptId), ct);

    public async Task<(List<Asset> Items, int Total)> SearchAsync(AssetQuery q, CancellationToken ct)
    {
        var query = db.Assets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            // Lower-cased on both sides so matching is case-insensitive on every provider (MySQL collation, InMemory, ...).
            var term = q.Search.Trim().ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(term) || a.Tag.ToLower().Contains(term) ||
                                     (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(term)));
        }
        if (q.Status is not null) query = query.Where(a => a.Status == q.Status);
        if (q.Category is not null) query = query.Where(a => a.Category == q.Category);

        var total = await query.CountAsync(ct);
        var ids = await query.OrderBy(a => a.Tag).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(a => a.Id).ToListAsync(ct);

        var items = await WithCurrent.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.Tag).ToListAsync(ct);
        return (items, total);
    }

    public Task<List<Asset>> ListForUserAsync(int userId, CancellationToken ct) =>
        WithCurrent.AsNoTracking()
            .Where(a => a.Assignments.Any(x => x.UserId == userId && x.ReturnedAt == null))
            .OrderBy(a => a.Tag).ToListAsync(ct);

    public Task<List<Assignment>> ListOverdueAsync(DateTime utcNow, CancellationToken ct) =>
        db.Assignments.AsNoTracking().Include(a => a.Asset).Include(a => a.User)
            .Where(a => a.ReturnedAt == null && a.DueAt < utcNow)
            .OrderBy(a => a.DueAt).ToListAsync(ct);

    public async Task<Dictionary<AssetStatus, int>> CountByStatusAsync(CancellationToken ct) =>
        await db.Assets.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<Dictionary<AssetCategory, int>> CountByCategoryAsync(CancellationToken ct) =>
        await db.Assets.GroupBy(a => a.Category).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<decimal> TotalActiveValueAsync(CancellationToken ct) =>
        await db.Assets.Where(a => a.Status != AssetStatus.Retired)
            .Select(a => (decimal?)a.PurchasePrice).SumAsync(ct) ?? 0m;

    public void Add(Asset asset) => db.Assets.Add(asset);
    public void Remove(Asset asset) => db.Assets.Remove(asset);
}
