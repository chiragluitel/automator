using AutoFlow.Application.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace AutoFlow.Infrastructure.Storage;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "minio:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "autoflow-assets";
    public bool UseSsl { get; set; } = false;
}

public class MinioAssetStorage : IAssetStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _opts;

    public MinioAssetStorage(IOptions<MinioOptions> opts)
    {
        _opts = opts.Value;
        _client = new MinioClient()
            .WithEndpoint(_opts.Endpoint)
            .WithCredentials(_opts.AccessKey, _opts.SecretKey)
            .WithSSL(_opts.UseSsl)
            .Build();
    }

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        var exists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_opts.Bucket), ct);
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_opts.Bucket), ct);
    }

    public async Task<string> PutAsync(byte[] data, string contentType, CancellationToken ct = default)
    {
        var ext = contentType.Contains("png") ? "png" : contentType.Contains("jpeg") ? "jpg" : "bin";
        var key = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}.{ext}";

        using var stream = new MemoryStream(data);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_opts.Bucket)
            .WithObject(key)
            .WithStreamData(stream)
            .WithObjectSize(data.Length)
            .WithContentType(contentType), ct);

        return key;
    }
}
