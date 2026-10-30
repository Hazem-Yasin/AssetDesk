using System.ComponentModel.DataAnnotations;
using AssetDesk.Domain.Enums;

namespace AssetDesk.Application.Dtos;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record RegisterRequest(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(100)] string DisplayName,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(int Id, string Email, string DisplayName, UserRole Role);

public record AssetDto(
    int Id, string Tag, string Name, AssetCategory Category, string? SerialNumber,
    DateOnly PurchaseDate, decimal PurchasePrice, string? Notes, AssetStatus Status,
    int Version, AssignmentDto? CurrentAssignment);

public record AssetDetailDto(
    AssetDto Asset, List<AssignmentDto> History, List<AttachmentDto> Attachments);

public record AssetRequest(
    [Required, RegularExpression("^[A-Za-z0-9-]{3,20}$", ErrorMessage = "Tag must be 3-20 letters, digits or dashes.")] string Tag,
    [Required, StringLength(150)] string Name,
    AssetCategory Category,
    [StringLength(100)] string? SerialNumber,
    DateOnly PurchaseDate,
    [Range(0, 1_000_000)] decimal PurchasePrice,
    [StringLength(1000)] string? Notes);

public record UpdateAssetRequest(
    [Required, StringLength(150)] string Name,
    AssetCategory Category,
    [StringLength(100)] string? SerialNumber,
    DateOnly PurchaseDate,
    [Range(0, 1_000_000)] decimal PurchasePrice,
    [StringLength(1000)] string? Notes,
    int Version);

public record CheckOutRequest(int UserId, DateTime DueAt, [StringLength(500)] string? Notes);

public record AssignmentDto(
    int Id, int AssetId, string AssetTag, string AssetName, int UserId, string UserName,
    DateTime CheckedOutAt, DateTime DueAt, DateTime? ReturnedAt, bool IsOverdue, string? Notes);

public record AttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, DateTime UploadedAt);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record StatsDto(
    int TotalAssets, int Overdue, decimal TotalValue,
    Dictionary<string, int> ByStatus, Dictionary<string, int> ByCategory);
