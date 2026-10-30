using AssetDesk.Application.Abstractions;
using AssetDesk.Application.Dtos;
using AssetDesk.Application.Services;
using AssetDesk.Domain.Enums;
using AssetDesk.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetDesk.Api.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
[Produces("application/json")]
public class AssetsController(AssetService assets, AttachmentService attachments) : ControllerBase
{
    /// <summary>Searches assets with filtering and paging.</summary>
    [HttpGet]
    public Task<PagedResult<AssetDto>> Search(
        [FromQuery] string? search, [FromQuery] AssetStatus? status, [FromQuery] AssetCategory? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        assets.SearchAsync(new AssetQuery(search, status, category, page, pageSize), ct);

    /// <summary>Gets an asset with its assignment history and attachments.</summary>
    [HttpGet("{id:int}")]
    public Task<AssetDetailDto> Get(int id, CancellationToken ct) => assets.GetAsync(id, ct);

    /// <summary>Assets currently checked out to the signed-in user.</summary>
    [HttpGet("mine")]
    public Task<List<AssetDto>> Mine(CancellationToken ct) => assets.ListForUserAsync(User.UserId(), ct);

    /// <summary>Inventory statistics for the dashboard.</summary>
    [HttpGet("stats")]
    public Task<StatsDto> Stats(CancellationToken ct) => assets.GetStatsAsync(ct);

    /// <summary>Open assignments that are past their due date (admin only).</summary>
    [HttpGet("overdue")]
    [Authorize(Roles = "Admin")]
    public Task<List<AssignmentDto>> Overdue(CancellationToken ct) => assets.ListOverdueAsync(ct);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<AssetDto>> Create(AssetRequest request, CancellationToken ct)
    {
        var created = await assets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<AssetDto> Update(int id, UpdateAssetRequest request, CancellationToken ct) =>
        assets.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await assets.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Checks an asset out to a user. Admins can assign to anyone; employees only to themselves.</summary>
    [HttpPost("{id:int}/checkout")]
    public Task<AssetDto> CheckOut(int id, CheckOutRequest request, CancellationToken ct)
    {
        if (!User.IsAdmin() && request.UserId != User.UserId())
            throw new ForbiddenException("Employees can only check assets out to themselves.");
        return assets.CheckOutAsync(id, request, ct);
    }

    /// <summary>Checks an asset back in. Employees can only return assets they hold.</summary>
    [HttpPost("{id:int}/checkin")]
    public async Task<AssetDto> CheckIn(int id, CancellationToken ct)
    {
        if (!User.IsAdmin())
        {
            var detail = await assets.GetAsync(id, ct);
            if (detail.Asset.CurrentAssignment?.UserId != User.UserId())
                throw new ForbiddenException("You can only return assets that are checked out to you.");
        }
        return await assets.CheckInAsync(id, ct);
    }

    [HttpPost("{id:int}/maintenance")]
    [Authorize(Roles = "Admin")]
    public Task<AssetDto> SendToMaintenance(int id, CancellationToken ct) => assets.SendToMaintenanceAsync(id, ct);

    [HttpPost("{id:int}/maintenance/complete")]
    [Authorize(Roles = "Admin")]
    public Task<AssetDto> CompleteMaintenance(int id, CancellationToken ct) => assets.ReturnFromMaintenanceAsync(id, ct);

    [HttpPost("{id:int}/retire")]
    [Authorize(Roles = "Admin")]
    public Task<AssetDto> Retire(int id, CancellationToken ct) => assets.RetireAsync(id, ct);

    /// <summary>Uploads an attachment (PDF/PNG/JPEG/text, max 5 MB) to the configured file store (local or S3).</summary>
    [HttpPost("{id:int}/attachments")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 1024 * 1024)]
    public async Task<ActionResult<AttachmentDto>> Upload(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var created = await attachments.UploadAsync(id, file.FileName, file.ContentType, file.Length, stream, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> Download(int id, int attachmentId, CancellationToken ct)
    {
        var (content, fileName, contentType) = await attachments.DownloadAsync(id, attachmentId, ct);
        return File(content, contentType, fileName);
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAttachment(int id, int attachmentId, CancellationToken ct)
    {
        await attachments.DeleteAsync(id, attachmentId, ct);
        return NoContent();
    }
}
