using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Enums;
using AssetDesk.Domain.Exceptions;

namespace AssetDesk.Tests.Unit;

public class AssetDomainTests
{
    private static readonly DateTime Now = new(2025, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly User Sara = new() { Id = 2, DisplayName = "Sara" };

    private static Asset NewAsset() => new() { Id = 1, Tag = "LT-1" };

    [Fact]
    public void CheckOut_Available_AssignsAndChangesStatus()
    {
        var asset = NewAsset();
        var assignment = asset.CheckOut(Sara, Now, Now.AddDays(7), "note");

        Assert.Equal(AssetStatus.CheckedOut, asset.Status);
        Assert.Same(assignment, asset.CurrentAssignment);
        Assert.Equal(2, assignment.UserId);
    }

    [Theory]
    [InlineData(AssetStatus.CheckedOut)]
    [InlineData(AssetStatus.InMaintenance)]
    [InlineData(AssetStatus.Retired)]
    public void CheckOut_WhenNotAvailable_Throws(AssetStatus status)
    {
        var asset = NewAsset();
        Move(asset, status);
        Assert.Throws<DomainException>(() => asset.CheckOut(Sara, Now, Now.AddDays(1), null));
    }

    [Fact]
    public void CheckOut_DueDateInPast_Throws()
    {
        Assert.Throws<DomainException>(() => NewAsset().CheckOut(Sara, Now, Now.AddMinutes(-1), null));
    }

    [Fact]
    public void CheckOut_LongerThanMaxLoan_Throws()
    {
        Assert.Throws<DomainException>(
            () => NewAsset().CheckOut(Sara, Now, Now.AddDays(Asset.MaxLoanDays + 1), null));
    }

    [Fact]
    public void CheckOut_ExactlyMaxLoan_IsAllowed()
    {
        var asset = NewAsset();
        asset.CheckOut(Sara, Now, Now.AddDays(Asset.MaxLoanDays), null);
        Assert.Equal(AssetStatus.CheckedOut, asset.Status);
    }

    [Fact]
    public void CheckIn_ClosesAssignmentAndFreesAsset()
    {
        var asset = NewAsset();
        var assignment = asset.CheckOut(Sara, Now, Now.AddDays(7), null);
        asset.CheckIn(Now.AddDays(2));

        Assert.Equal(AssetStatus.Available, asset.Status);
        Assert.Equal(Now.AddDays(2), assignment.ReturnedAt);
        Assert.Null(asset.CurrentAssignment);
    }

    [Fact]
    public void CheckIn_WhenNotCheckedOut_Throws()
    {
        Assert.Throws<DomainException>(() => NewAsset().CheckIn(Now));
    }

    [Fact]
    public void Assignment_IsOverdue_OnlyWhileOpenAndPastDue()
    {
        var asset = NewAsset();
        var assignment = asset.CheckOut(Sara, Now, Now.AddDays(1), null);

        Assert.False(assignment.IsOverdue(Now.AddHours(1)));
        Assert.True(assignment.IsOverdue(Now.AddDays(2)));

        asset.CheckIn(Now.AddDays(2));
        Assert.False(assignment.IsOverdue(Now.AddDays(3)));
    }

    [Fact]
    public void Maintenance_RoundTrip()
    {
        var asset = NewAsset();
        asset.SendToMaintenance();
        Assert.Equal(AssetStatus.InMaintenance, asset.Status);
        asset.ReturnFromMaintenance();
        Assert.Equal(AssetStatus.Available, asset.Status);
    }

    [Fact]
    public void SendToMaintenance_WhenCheckedOut_Throws()
    {
        var asset = NewAsset();
        asset.CheckOut(Sara, Now, Now.AddDays(1), null);
        Assert.Throws<DomainException>(asset.SendToMaintenance);
    }

    [Fact]
    public void Retire_WhenCheckedOut_Throws()
    {
        var asset = NewAsset();
        asset.CheckOut(Sara, Now, Now.AddDays(1), null);
        Assert.Throws<DomainException>(asset.Retire);
    }

    [Fact]
    public void Retire_Twice_Throws_AndRetiredAssetIsNotEditable()
    {
        var asset = NewAsset();
        asset.Retire();
        Assert.Throws<DomainException>(asset.Retire);
        Assert.Throws<DomainException>(asset.EnsureEditable);
    }

    private static void Move(Asset asset, AssetStatus status)
    {
        switch (status)
        {
            case AssetStatus.CheckedOut: asset.CheckOut(Sara, Now, Now.AddDays(1), null); break;
            case AssetStatus.InMaintenance: asset.SendToMaintenance(); break;
            case AssetStatus.Retired: asset.Retire(); break;
        }
    }
}
