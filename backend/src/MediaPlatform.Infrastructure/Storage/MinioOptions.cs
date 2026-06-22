namespace MediaPlatform.Infrastructure.Storage;

/// <summary>Configuration MinIO (section "Minio" de la configuration).</summary>
public class MinioOptions
{
    public string Endpoint { get; set; } = "minio:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool UseSsl { get; set; } = false;
    public string OriginalsBucket { get; set; } = "originals";
    public string HlsBucket { get; set; } = "hls";
}
