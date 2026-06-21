using MediaPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace MediaPlatform.Infrastructure.Storage;

/// <summary>Implémentation MinIO d'<see cref="IObjectStorage"/> (SDK Minio .NET 6).</summary>
public class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;

    public MinioObjectStorage(IOptions<MinioOptions> options)
    {
        var o = options.Value;
        _client = new MinioClient()
            .WithEndpoint(o.Endpoint)
            .WithCredentials(o.AccessKey, o.SecretKey)
            .WithSSL(o.UseSsl)
            .Build();
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
    }

    public Task PutAsync(string bucket, string key, Stream content, long size, string contentType, CancellationToken ct = default) =>
        _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithStreamData(content).WithObjectSize(size)
            .WithContentType(contentType), ct);

    public async Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithCallbackStream(async (s, c) => await s.CopyToAsync(ms, c)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task GetToFileAsync(string bucket, string key, string destPath, CancellationToken ct = default)
    {
        await using var file = File.Create(destPath);
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithCallbackStream(async (s, c) => await s.CopyToAsync(file, c)), ct);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        var keys = new List<string>();
        var args = new ListObjectsArgs().WithBucket(bucket).WithPrefix(prefix).WithRecursive(true);
        await foreach (var item in _client.ListObjectsEnumAsync(args, ct))
            keys.Add(item.Key);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    public async Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        foreach (var key in await ListKeysAsync(bucket, prefix, ct))
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), ct);
    }

    public Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default) =>
        _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket).WithObject(key).WithExpiry((int)expiry.TotalSeconds));
}
