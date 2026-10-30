using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssetDesk.Application.Dtos;
using AssetDesk.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssetDesk.Tests.Integration;

/// <summary>
/// Boots the real API. Uses InMemory by default; set ASSETDESK_TEST_MYSQL to a MySQL connection string
/// (the database is dropped first) to run the same tests against a real MySQL server.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminPassword = "Admin#12345";
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "assetdesk-up-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var mysql = Environment.GetEnvironmentVariable("ASSETDESK_TEST_MYSQL");
        builder.UseSetting("Database:Provider", mysql is null ? "InMemory" : "MySql");
        builder.UseSetting("ConnectionStrings:Default", mysql ?? $"tests-{Guid.NewGuid()}");
        builder.UseSetting("Database:Seed", "true");
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Jwt:Key", "integration-test-key-integration-test-key-0123456789");
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Storage:LocalPath", _uploads);
        builder.UseSetting("RateLimit:AuthPermitPerMinute", "1000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_uploads)) Directory.Delete(_uploads, recursive: true);
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static HttpClient Anonymous(ApiFactory f) => f.CreateClient();

    private async Task<HttpClient> SignIn(string email, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    private Task<HttpClient> Admin() => SignIn("admin@assetdesk.local", ApiFactory.AdminPassword);
    private Task<HttpClient> Sara() => SignIn("sara@assetdesk.local", "Employee#123");

    private static async Task<T> Read<T>(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static AssetRequest NewAsset(string tag) =>
        new(tag, "Test " + tag, AssetCategory.Laptop, "SN-" + tag, new DateOnly(2025, 1, 1), 999m, null);

    private static async Task<AssetDto> Create(HttpClient admin, string tag) =>
        await Read<AssetDto>(await admin.PostAsJsonAsync("/api/assets", NewAsset(tag), Json));

    [Fact]
    public async Task Health_IsHealthy()
    {
        Assert.Equal(HttpStatusCode.OK, (await Anonymous(factory).GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Is404_NotTheSpaShell()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Anonymous(factory).GetAsync("/api/does-not-exist")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous(factory).GetAsync("/api/assets")).StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var r = await Anonymous(factory).PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@assetdesk.local", "wrong"), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesEmployee_WhoCannotUseAdminEndpoints()
    {
        var email = $"new{Guid.NewGuid():N}@example.com";
        var r = await Anonymous(factory).PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "New User", "password123"), Json);
        var auth = await Read<AuthResponse>(r);
        Assert.Equal(UserRole.Employee, auth.User.Role);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/assets/overdue")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/assets", NewAsset("NOPE-1"), Json)).StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var r = await Anonymous(factory).PostAsJsonAsync("/api/auth/register", new RegisterRequest("a@b.com", "A", "short"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreate_ButDuplicateTagIsRejected_CaseInsensitively()
    {
        var admin = await Admin();
        await Create(admin, "DUP-1");
        var r = await admin.PostAsJsonAsync("/api/assets", NewAsset("dup-1"), Json);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Search_FiltersAndPages()
    {
        var sara = await Sara();
        var page = await Read<PagedResult<AssetDto>>(await sara.GetAsync("/api/assets?search=lenovo&pageSize=1&page=1"));
        Assert.True(page.Total >= 2);
        Assert.Single(page.Items);

        var byStatus = await Read<PagedResult<AssetDto>>(await sara.GetAsync("/api/assets?status=InMaintenance"));
        Assert.All(byStatus.Items, a => Assert.Equal(AssetStatus.InMaintenance, a.Status));
    }

    [Fact]
    public async Task Employee_CanOnlyCheckOutToSelf_AndOnlyReturnOwn()
    {
        var admin = await Admin();
        var sara = await Sara();
        var asset = await Create(admin, "CHK-1");
        var due = DateTime.UtcNow.AddDays(5);

        var users = await Read<List<UserDto>>(await admin.GetAsync("/api/auth/users"));
        var saraId = users.Single(u => u.Email == "sara@assetdesk.local").Id;
        var omarId = users.Single(u => u.Email == "omar@assetdesk.local").Id;

        Assert.Equal(HttpStatusCode.Forbidden,
            (await sara.PostAsJsonAsync($"/api/assets/{asset.Id}/checkout", new CheckOutRequest(omarId, due, null), Json)).StatusCode);

        var checkedOut = await Read<AssetDto>(
            await sara.PostAsJsonAsync($"/api/assets/{asset.Id}/checkout", new CheckOutRequest(saraId, due, null), Json));
        Assert.Equal(AssetStatus.CheckedOut, checkedOut.Status);
        Assert.Equal(saraId, checkedOut.CurrentAssignment!.UserId);

        Assert.Equal(HttpStatusCode.Conflict,
            (await sara.PostAsJsonAsync($"/api/assets/{asset.Id}/checkout", new CheckOutRequest(saraId, due, null), Json)).StatusCode);

        var mine = await Read<List<AssetDto>>(await sara.GetAsync("/api/assets/mine"));
        Assert.Contains(mine, a => a.Id == asset.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/assets/{asset.Id}/retire", null)).StatusCode);
        var returned = await Read<AssetDto>(await sara.PostAsync($"/api/assets/{asset.Id}/checkin", null));
        Assert.Equal(AssetStatus.Available, returned.Status);

        var detail = await Read<AssetDetailDto>(await sara.GetAsync($"/api/assets/{asset.Id}"));
        Assert.Single(detail.History);
        Assert.NotNull(detail.History[0].ReturnedAt);
    }

    [Fact]
    public async Task Employee_CannotReturnAnotherUsersAsset()
    {
        var admin = await Admin();
        var sara = await Sara();
        var users = await Read<List<UserDto>>(await admin.GetAsync("/api/auth/users"));
        var omarId = users.Single(u => u.Email == "omar@assetdesk.local").Id;
        var asset = await Create(admin, "OMAR-1");
        await Read<AssetDto>(await admin.PostAsJsonAsync($"/api/assets/{asset.Id}/checkout",
            new CheckOutRequest(omarId, DateTime.UtcNow.AddDays(3), null), Json));

        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PostAsync($"/api/assets/{asset.Id}/checkin", null)).StatusCode);
    }

    [Fact]
    public async Task Overdue_ListsSeededOverdueLoan_ForAdminOnly()
    {
        var admin = await Admin();
        var overdue = await Read<List<AssignmentDto>>(await admin.GetAsync("/api/assets/overdue"));
        Assert.Contains(overdue, a => a.AssetTag == "LT-0002" && a.IsOverdue);

        Assert.Equal(HttpStatusCode.Forbidden, (await (await Sara()).GetAsync("/api/assets/overdue")).StatusCode);
    }

    [Fact]
    public async Task Update_UsesOptimisticConcurrency()
    {
        var admin = await Admin();
        var asset = await Create(admin, "CON-1");
        UpdateAssetRequest Update(string name, int version) =>
            new(name, AssetCategory.Monitor, null, new DateOnly(2025, 1, 1), 100m, null, version);

        var first = await Read<AssetDto>(await admin.PutAsJsonAsync($"/api/assets/{asset.Id}", Update("First", asset.Version), Json));
        Assert.Equal(asset.Version + 1, first.Version);
        Assert.Equal("First", first.Name);

        var stale = await admin.PutAsJsonAsync($"/api/assets/{asset.Id}", Update("Stale", asset.Version), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Stats_ReportTotalsAndValue()
    {
        var stats = await Read<StatsDto>(await (await Sara()).GetAsync("/api/assets/stats"));
        Assert.True(stats.TotalAssets >= 6);
        Assert.True(stats.TotalValue > 0);
        Assert.True(stats.Overdue >= 1);
        Assert.True(stats.ByStatus.ContainsKey("CheckedOut"));
    }

    [Fact]
    public async Task Attachment_UploadDownloadDelete_AndRejectsBadTypes()
    {
        var admin = await Admin();
        var asset = await Create(admin, "ATT-1");

        using var good = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        good.Add(bytes, "file", "receipt.pdf");
        var uploaded = await Read<AttachmentDto>(await admin.PostAsync($"/api/assets/{asset.Id}/attachments", good));
        Assert.Equal("receipt.pdf", uploaded.FileName);

        var download = await (await Sara()).GetAsync($"/api/assets/{asset.Id}/attachments/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("%PDF-1.4 test", await download.Content.ReadAsStringAsync());

        using var bad = new MultipartFormDataContent();
        var exe = new ByteArrayContent([1, 2, 3]);
        exe.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        bad.Add(exe, "file", "virus.exe");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/assets/{asset.Id}/attachments", bad)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await Sara()).DeleteAsync($"/api/assets/{asset.Id}/attachments/{uploaded.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"/api/assets/{asset.Id}/attachments/{uploaded.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/assets/{asset.Id}/attachments/{uploaded.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_BlockedWhenAssetHasHistory_AllowedOtherwise()
    {
        var admin = await Admin();
        var fresh = await Create(admin, "DEL-1");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/assets/{fresh.Id}")).StatusCode);

        var seeded = await Read<PagedResult<AssetDto>>(await admin.GetAsync("/api/assets?search=LT-0001"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/assets/{seeded.Items[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task UnknownAsset_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await (await Sara()).GetAsync("/api/assets/999999")).StatusCode);
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task LoginIsRateLimited_ToBlockBruteForce()
    {
        await using var factory = new LimitedFactory();
        var client = factory.CreateClient();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            codes.Add((await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("x@y.com", "bad"))).StatusCode);

        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.Unauthorized));
        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.TooManyRequests));
    }

    private sealed class LimitedFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Database:Provider", "InMemory");
            builder.UseSetting("ConnectionStrings:Default", $"rl-{Guid.NewGuid()}");
            builder.UseSetting("Database:Seed", "false");
            builder.UseSetting("Jwt:Key", "integration-test-key-integration-test-key-0123456789");
            builder.UseSetting("RateLimit:AuthPermitPerMinute", "3");
        }
    }
}
