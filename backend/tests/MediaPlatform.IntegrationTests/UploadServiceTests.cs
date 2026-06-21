using System.Text;
using FluentAssertions;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class UploadServiceTests : IClassFixture<MinioFixture>
{
    private readonly UploadService _svc;

    public UploadServiceTests(MinioFixture fx)
    {
        var storage = new MinioObjectStorage(Options.Create(new MinioOptions
        {
            Endpoint = fx.Endpoint, AccessKey = fx.AccessKey, SecretKey = fx.SecretKey, UseSsl = false
        }));
        _svc = new UploadService(storage, Options.Create(new MinioOptions()));
    }

    private static Stream Chunk(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task StoreChunk_is_idempotent_and_tracks_indices()
    {
        var id = Guid.NewGuid();
        await _svc.StoreChunkAsync(id, 0, Chunk("aaa"), 3);
        await _svc.StoreChunkAsync(id, 1, Chunk("bbb"), 3);
        var received = await _svc.StoreChunkAsync(id, 1, Chunk("bbb"), 3); // re-post

        received.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Complete_throws_when_a_chunk_is_missing()
    {
        var id = Guid.NewGuid();
        await _svc.StoreChunkAsync(id, 0, Chunk("aaa"), 3);
        var act = async () => await _svc.CompleteAsync(id, total: 2);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Complete_returns_parts_prefix_when_all_present()
    {
        var id = Guid.NewGuid();
        await _svc.StoreChunkAsync(id, 0, Chunk("aaa"), 3);
        await _svc.StoreChunkAsync(id, 1, Chunk("bbb"), 3);
        (await _svc.CompleteAsync(id, total: 2)).Should().Be(IUploadService.PartsPrefix(id));
    }
}
