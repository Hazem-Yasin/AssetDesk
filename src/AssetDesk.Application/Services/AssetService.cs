using AssetDesk.Application.Abstractions;
using AssetDesk.Application.Dtos;
using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Exceptions;

namespace AssetDesk.Application.Services;

public class AssetService(IAssetRepository assets, IUserRepository users, IUnitOfWork uow, IClock clock)
{
    public const int MaxPageSize = 100;

    public async Task<PagedResult<AssetDto>> SearchAsync(AssetQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await assets.SearchAsync(query with { Page = page, PageSize = size }, ct);
        var now = clock.UtcNow;
        return new PagedResult<AssetDto>(items.Select(a => Mapper.ToDto(a, now)).ToList(), total, page, size);
    }

    public async Task<AssetDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(id, ct) ?? throw new NotFoundException(nameof(Asset), id);
        var now = clock.UtcNow;
        return new AssetDetailDto(
            Mapper.ToDto(asset, now),
            asset.Assignments.OrderByDescending(a => a.CheckedOutAt).Select(a => Mapper.ToDto(a, now)).ToList(),
            asset.Attachments.OrderByDescending(a => a.UploadedAt).Select(Mapper.ToDto).ToList());
    }

    public async Task<AssetDto> CreateAsync(AssetRequest request, CancellationToken ct)
    {
        var tag = request.Tag.Trim().ToUpperInvariant();
        if (await assets.TagExistsAsync(tag, null, ct))
            throw new DomainException($"An asset with tag '{tag}' already exists.");

        var asset = new Asset
        {
            Tag = tag,
            Name = request.Name.Trim(),
            Category = request.Category,
            SerialNumber = Clean(request.SerialNumber),
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            Notes = Clean(request.Notes),
            CreatedAt = clock.UtcNow
        };
        assets.Add(asset);
        await uow.SaveChangesAsync(ct);
        return Mapper.ToDto(asset, clock.UtcNow);
    }

    public async Task<AssetDto> UpdateAsync(int id, UpdateAssetRequest request, CancellationToken ct)
    {
        var asset = await assets.GetAsync(id, ct) ?? throw new NotFoundException(nameof(Asset), id);
        asset.EnsureEditable();
        if (asset.Version != request.Version)
            throw new DomainException("The asset was changed by someone else. Reload it and try again.");

        asset.Name = request.Name.Trim();
        asset.Category = request.Category;
        asset.SerialNumber = Clean(request.SerialNumber);
        asset.PurchaseDate = request.PurchaseDate;
        asset.PurchasePrice = request.PurchasePrice;
        asset.Notes = Clean(request.Notes);
        await uow.SaveChangesAsync(ct);
        return Mapper.ToDto(asset, clock.UtcNow);
    }

    public async Task<AssetDto> CheckOutAsync(int id, CheckOutRequest request, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(id, ct) ?? throw new NotFoundException(nameof(Asset), id);
        var user = await users.GetAsync(request.UserId, ct) ?? throw new NotFoundException(nameof(User), request.UserId);
        asset.CheckOut(user, clock.UtcNow, request.DueAt.ToUniversalTime(), Clean(request.Notes));
        await uow.SaveChangesAsync(ct);
        return Mapper.ToDto(asset, clock.UtcNow);
    }

    public async Task<AssetDto> CheckInAsync(int id, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(id, ct) ?? throw new NotFoundException(nameof(Asset), id);
        asset.CheckIn(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Mapper.ToDto(asset, clock.UtcNow);
    }

    public Task<AssetDto> SendToMaintenanceAsync(int id, CancellationToken ct) =>
        ChangeAsync(id, a => a.SendToMaintenance(), ct);

    public Task<AssetDto> ReturnFromMaintenanceAsync(int id, CancellationToken ct) =>
        ChangeAsync(id, a => a.ReturnFromMaintenance(), ct);

    public Task<AssetDto> RetireAsync(int id, CancellationToken ct) =>
        ChangeAsync(id, a => a.Retire(), ct);

    public async Task<List<AssetDto>> ListForUserAsync(int userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        return (await assets.ListForUserAsync(userId, ct)).Select(a => Mapper.ToDto(a, now)).ToList();
    }

    public async Task<List<AssignmentDto>> ListOverdueAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        return (await assets.ListOverdueAsync(now, ct)).Select(a => Mapper.ToDto(a, now)).ToList();
    }

    public async Task<StatsDto> GetStatsAsync(CancellationToken ct)
    {
        var byStatus = await assets.CountByStatusAsync(ct);
        var byCategory = await assets.CountByCategoryAsync(ct);
        var overdue = (await assets.ListOverdueAsync(clock.UtcNow, ct)).Count;
        return new StatsDto(
            byStatus.Values.Sum(), overdue, await assets.TotalActiveValueAsync(ct),
            byStatus.ToDictionary(k => k.Key.ToString(), v => v.Value),
            byCategory.ToDictionary(k => k.Key.ToString(), v => v.Value));
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(id, ct) ?? throw new NotFoundException(nameof(Asset), id);
        if (asset.Assignments.Count > 0)
            throw new DomainException("Assets with assignment history cannot be deleted; retire them instead.");
        assets.Remove(asset);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<AssetDto> ChangeAsync(int id, Action<Asset> change, CancellationToken ct)
    {
        var asset = await assets.GetDetailAsync(id, ct) ?? throw new NotFoundException(nameof(Asset), id);
        change(asset);
        await uow.SaveChangesAsync(ct);
        return Mapper.ToDto(asset, clock.UtcNow);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
