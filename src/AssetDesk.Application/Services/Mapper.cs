using AssetDesk.Application.Dtos;
using AssetDesk.Domain.Entities;

namespace AssetDesk.Application.Services;

internal static class Mapper
{
    public static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role);

    public static AssignmentDto ToDto(Assignment a, DateTime now) => new(
        a.Id, a.AssetId, a.Asset?.Tag ?? string.Empty, a.Asset?.Name ?? string.Empty,
        a.UserId, a.User?.DisplayName ?? string.Empty,
        a.CheckedOutAt, a.DueAt, a.ReturnedAt, a.IsOverdue(now), a.Notes);

    public static AssetDto ToDto(Asset a, DateTime now)
    {
        var current = a.CurrentAssignment;
        return new AssetDto(
            a.Id, a.Tag, a.Name, a.Category, a.SerialNumber, a.PurchaseDate, a.PurchasePrice,
            a.Notes, a.Status, a.Version, current is null ? null : ToDto(current, now));
    }

    public static AttachmentDto ToDto(Attachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedAt);
}
