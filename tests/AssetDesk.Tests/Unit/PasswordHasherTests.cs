using AssetDesk.Infrastructure.Security;

namespace AssetDesk.Tests.Unit;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = _hasher.Hash("S3cret#Pass");
        Assert.True(_hasher.Verify("S3cret#Pass", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        Assert.False(_hasher.Verify("nope", _hasher.Hash("S3cret#Pass")));
    }

    [Fact]
    public void Hash_IsSalted_SamePasswordGivesDifferentHashes()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1.abc.def.ghi")]
    public void Verify_MalformedHash_ReturnsFalse(string hash)
    {
        Assert.False(_hasher.Verify("x", hash));
    }
}
