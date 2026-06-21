using Minio;
using Testcontainers.Minio;
using Xunit;

namespace MediaPlatform.IntegrationTests.Fixtures;

/// <summary>Démarre un conteneur MinIO partagé pour les tests de stockage.</summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private readonly MinioContainer _container = new MinioBuilder()
        .WithImage("minio/minio:latest")
        .Build();

    public string Endpoint => _container.GetConnectionString().Replace("http://", "");

    // Le module Testcontainers.Minio utilise des identifiants par défaut propres
    // (pas minioadmin) ; on les lit directement depuis le conteneur pour rester
    // robuste à la version du module.
    public string AccessKey => _container.GetAccessKey();
    public string SecretKey => _container.GetSecretKey();

    public IMinioClient CreateClient() =>
        new MinioClient().WithEndpoint(Endpoint).WithCredentials(AccessKey, SecretKey).Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
