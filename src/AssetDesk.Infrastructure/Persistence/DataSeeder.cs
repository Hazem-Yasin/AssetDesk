using AssetDesk.Application.Abstractions;
using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssetDesk.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, string adminPassword)
    {
        if (await db.Users.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var admin = new User
        {
            Email = "admin@assetdesk.local", DisplayName = "Admin", Role = UserRole.Admin,
            PasswordHash = hasher.Hash(adminPassword), CreatedAt = now
        };
        var sara = new User
        {
            Email = "sara@assetdesk.local", DisplayName = "Sara Ahmed", Role = UserRole.Employee,
            PasswordHash = hasher.Hash("Employee#123"), CreatedAt = now
        };
        var omar = new User
        {
            Email = "omar@assetdesk.local", DisplayName = "Omar Hassan", Role = UserRole.Employee,
            PasswordHash = hasher.Hash("Employee#123"), CreatedAt = now
        };

        Asset Make(string tag, string name, AssetCategory cat, decimal price, int monthsAgo) => new()
        {
            Tag = tag, Name = name, Category = cat, PurchasePrice = price, CreatedAt = now,
            SerialNumber = $"SN-{tag}", PurchaseDate = DateOnly.FromDateTime(now.AddMonths(-monthsAgo))
        };

        var a1 = Make("LT-0001", "Lenovo ThinkPad T14", AssetCategory.Laptop, 1450m, 10);
        var a2 = Make("LT-0002", "Lenovo ThinkPad X1 Carbon", AssetCategory.Laptop, 2100m, 6);
        var a3 = Make("MN-0001", "Dell U2723QE 27\" monitor", AssetCategory.Monitor, 620m, 14);
        var a4 = Make("PH-0001", "iPhone 14", AssetCategory.Phone, 900m, 20);
        var a5 = Make("DT-0001", "HP EliteDesk 800", AssetCategory.Desktop, 1100m, 30);
        var a6 = Make("PR-0001", "Logitech MX Keys + Mouse", AssetCategory.Peripheral, 180m, 3);

        a1.CheckOut(sara, now.AddDays(-20), now.AddDays(40), "Remote work laptop");
        a2.CheckOut(omar, now.AddDays(-30), now.AddDays(-5), "Project loan"); // overdue
        a5.SendToMaintenance();

        db.Users.AddRange(admin, sara, omar);
        db.Assets.AddRange(a1, a2, a3, a4, a5, a6);
        await db.SaveChangesAsync();
    }
}
