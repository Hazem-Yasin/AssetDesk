using AssetDesk.Domain.Enums;
using AssetDesk.Domain.Exceptions;

namespace AssetDesk.Domain.Entities;

/// <summary>Asset aggregate: owns its lifecycle rules (check-out, check-in, maintenance, retirement).</summary>
public class Asset
{
    public const int MaxLoanDays = 90;

    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetCategory Category { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public AssetStatus Status { get; private set; } = AssetStatus.Available;

    /// <summary>Optimistic concurrency token; incremented by the DbContext on every update.</summary>
    public int Version { get; private set; } = 1;

    public List<Assignment> Assignments { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];

    public Assignment? CurrentAssignment => Assignments.FirstOrDefault(a => a.IsActive);

    public Assignment CheckOut(User user, DateTime now, DateTime dueAt, string? notes)
    {
        if (Status != AssetStatus.Available)
            throw new DomainException($"Asset {Tag} is {Status} and cannot be checked out.");
        if (dueAt <= now)
            throw new DomainException("The due date must be in the future.");
        if (dueAt > now.AddDays(MaxLoanDays))
            throw new DomainException($"Loans are limited to {MaxLoanDays} days.");

        var assignment = new Assignment
        {
            Asset = this,
            AssetId = Id,
            User = user,
            UserId = user.Id,
            CheckedOutAt = now,
            DueAt = dueAt,
            Notes = notes
        };
        Assignments.Add(assignment);
        Status = AssetStatus.CheckedOut;
        return assignment;
    }

    public Assignment CheckIn(DateTime now)
    {
        var current = Status == AssetStatus.CheckedOut ? CurrentAssignment : null;
        if (current is null)
            throw new DomainException($"Asset {Tag} is not checked out.");

        current.ReturnedAt = now;
        Status = AssetStatus.Available;
        return current;
    }

    public void SendToMaintenance()
    {
        if (Status != AssetStatus.Available)
            throw new DomainException("Only an available asset can be sent to maintenance.");
        Status = AssetStatus.InMaintenance;
    }

    public void ReturnFromMaintenance()
    {
        if (Status != AssetStatus.InMaintenance)
            throw new DomainException("The asset is not in maintenance.");
        Status = AssetStatus.Available;
    }

    public void Retire()
    {
        if (Status is AssetStatus.CheckedOut)
            throw new DomainException("A checked-out asset must be checked in before it is retired.");
        if (Status == AssetStatus.Retired)
            throw new DomainException("The asset is already retired.");
        Status = AssetStatus.Retired;
    }

    public void EnsureEditable()
    {
        if (Status == AssetStatus.Retired)
            throw new DomainException("Retired assets cannot be modified.");
    }
}
