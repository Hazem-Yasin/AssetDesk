using AssetDesk.Application.Abstractions;
using AssetDesk.Application.Dtos;
using AssetDesk.Application.Services;
using AssetDesk.Domain.Entities;
using AssetDesk.Domain.Enums;
using AssetDesk.Domain.Exceptions;
using Moq;

namespace AssetDesk.Tests.Unit;

public class AttachmentServiceTests
{
    private readonly Mock<IAssetRepository> _assets = new();
    private readonly Mock<IFileStore> _files = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IClock> _clock = new();

    private AttachmentService Create()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _assets.Setup(a => a.GetDetailAsync(1, default)).ReturnsAsync(new Asset { Id = 1, Tag = "A-1" });
        return new AttachmentService(_assets.Object, _files.Object, _uow.Object, _clock.Object);
    }

    private static MemoryStream Bytes(int n = 10) => new(new byte[n]);

    [Fact]
    public async Task Upload_Valid_StoresFileWithGeneratedKey_AndNeverTrustsClientPath()
    {
        string? savedKey = null;
        var service = Create();
        _files.Setup(f => f.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), "application/pdf", default))
            .Callback<string, Stream, string, CancellationToken>((k, _, _, _) => savedKey = k)
            .Returns(Task.CompletedTask);

        var dto = await service.UploadAsync(1, @"..\..\evil\invoice.pdf", "application/pdf", 10, Bytes(), default);

        Assert.Equal("invoice.pdf", dto.FileName);
        Assert.StartsWith("assets/1/", savedKey);
        Assert.DoesNotContain("evil", savedKey);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Upload_TooLarge_Throws_AndStoresNothing()
    {
        var service = Create();
        await Assert.ThrowsAsync<DomainException>(
            () => service.UploadAsync(1, "a.pdf", "application/pdf", AttachmentService.MaxBytes + 1, Bytes(), default));
        _files.Verify(f => f.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), default), Times.Never);
    }

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("text/html")]
    [InlineData("image/svg+xml")]
    public async Task Upload_DisallowedType_Throws(string contentType)
    {
        var service = Create();
        await Assert.ThrowsAsync<DomainException>(
            () => service.UploadAsync(1, "a.bin", contentType, 10, Bytes(), default));
    }

    [Fact]
    public async Task Upload_EmptyFile_Throws()
    {
        var service = Create();
        await Assert.ThrowsAsync<DomainException>(
            () => service.UploadAsync(1, "a.pdf", "application/pdf", 0, Bytes(0), default));
    }

    [Fact]
    public async Task Upload_WhenDatabaseSaveFails_RemovesTheStoredObject()
    {
        var service = Create();
        _uow.Setup(u => u.SaveChangesAsync(default)).ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UploadAsync(1, "a.pdf", "application/pdf", 10, Bytes(), default));

        _files.Verify(f => f.DeleteAsync(It.Is<string>(k => k.StartsWith("assets/1/")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_ToRetiredAsset_Throws()
    {
        var retired = new Asset { Id = 2, Tag = "R-1" };
        retired.Retire();
        var service = Create();
        _assets.Setup(a => a.GetDetailAsync(2, default)).ReturnsAsync(retired);

        await Assert.ThrowsAsync<DomainException>(
            () => service.UploadAsync(2, "a.pdf", "application/pdf", 10, Bytes(), default));
    }
}

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IClock> _clock = new();

    private AuthService Create()
    {
        _tokens.Setup(t => t.Create(It.IsAny<User>())).Returns(("jwt", DateTime.UtcNow.AddHours(1)));
        return new AuthService(_users.Object, _hasher.Object, _tokens.Object, _uow.Object, _clock.Object);
    }

    [Fact]
    public async Task Register_AlwaysCreatesEmployee_NeverAdmin()
    {
        _hasher.Setup(h => h.Hash("password1")).Returns("hashed");
        User? added = null;
        _users.Setup(u => u.Add(It.IsAny<User>())).Callback<User>(u => added = u);

        var response = await Create().RegisterAsync(new RegisterRequest("  New@Example.com ", "New", "password1"), default);

        Assert.Equal(UserRole.Employee, added!.Role);
        Assert.Equal("new@example.com", added.Email);
        Assert.Equal("hashed", added.PasswordHash);
        Assert.Equal("jwt", response.Token);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        _users.Setup(u => u.GetByEmailAsync("a@b.com", default)).ReturnsAsync(new User());
        await Assert.ThrowsAsync<DomainException>(
            () => Create().RegisterAsync(new RegisterRequest("a@b.com", "A", "password1"), default));
    }

    [Fact]
    public async Task Login_UnknownUser_And_WrongPassword_GiveTheSameError()
    {
        var service = Create();
        var unknown = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(new LoginRequest("none@x.com", "pw"), default));

        _users.Setup(u => u.GetByEmailAsync("a@b.com", default)).ReturnsAsync(new User { PasswordHash = "h" });
        _hasher.Setup(h => h.Verify("bad", "h")).Returns(false);
        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(new LoginRequest("a@b.com", "bad"), default));

        Assert.Equal(unknown.Message, wrong.Message); // no user enumeration
    }
}
