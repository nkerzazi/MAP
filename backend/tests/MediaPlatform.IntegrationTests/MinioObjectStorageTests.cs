using System.Text;
using FluentAssertions;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class MinioObjectStorageTests : IClassFixture<MinioFixture>
{
    private readonly MinioObjectStorage _storage;
    private const string Bucket = "originals";

    public MinioObjectStorageTests(MinioFixture fx)
    {
        var opts = Options.Create(new MinioOptions
        {
            Endpoint = fx.Endpoint, AccessKey = fx.AccessKey, SecretKey = fx.SecretKey, UseSsl = false
        });
        _storage = new MinioObjectStorage(opts);
    }

    [Fact]
    public async Task Put_then_list_and_get_roundtrips()
    {
        await _storage.EnsureBucketAsync(Bucket);
        var key = $"originals/{Guid.NewGuid()}/parts/000000";
        var bytes = Encoding.UTF8.GetBytes("hello-chunk");
        using (var ms = new MemoryStream(bytes))
            await _storage.PutAsync(Bucket, key, ms, bytes.Length, "application/octet-stream");

        var keys = await _storage.ListKeysAsync(Bucket, key[..key.LastIndexOf('/')] + "/");
        keys.Should().ContainSingle().Which.Should().Be(key);

        using var got = await _storage.GetAsync(Bucket, key);
        using var reader = new StreamReader(got);
        (await reader.ReadToEndAsync()).Should().Be("hello-chunk");
    }

    [Fact]
    public async Task DeletePrefix_removes_all_objects_under_prefix()
    {
        await _storage.EnsureBucketAsync(Bucket);
        var prefix = $"originals/{Guid.NewGuid()}/parts/";
        foreach (var i in Enumerable.Range(0, 3))
        {
            var b = Encoding.UTF8.GetBytes($"part{i}");
            using var ms = new MemoryStream(b);
            await _storage.PutAsync(Bucket, $"{prefix}{i:000000}", ms, b.Length, "application/octet-stream");
        }

        await _storage.DeletePrefixAsync(Bucket, prefix);

        (await _storage.ListKeysAsync(Bucket, prefix)).Should().BeEmpty();
    }
}
