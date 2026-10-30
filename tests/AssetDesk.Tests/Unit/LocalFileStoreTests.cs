using AssetDesk.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace AssetDesk.Tests.Unit;

public class LocalFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "assetdesk-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStore _store;

    public LocalFileStoreTests()
    {
        _store = new LocalFileStore(Options.Create(new StorageOptions { LocalPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Save_Open_Delete_RoundTrip()
    {
        await _store.SaveAsync("assets/1/abc", new MemoryStream("hello"u8.ToArray()), "text/plain", default);

        await using (var stream = await _store.OpenAsync("assets/1/abc", default))
        using (var reader = new StreamReader(stream))
            Assert.Equal("hello", await reader.ReadToEndAsync());

        await _store.DeleteAsync("assets/1/abc", default);
        await Assert.ThrowsAsync<FileNotFoundException>(() => _store.OpenAsync("assets/1/abc", default));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("assets/../../outside")]
    public async Task PathTraversal_IsRejected(string key)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.SaveAsync(key, new MemoryStream([1]), "text/plain", default));
    }

    [Fact]
    public async Task Delete_MissingFile_IsNotAnError()
    {
        await _store.DeleteAsync("assets/9/none", default);
    }
}
