using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediaPlatform.Infrastructure.Persistence;

/// <summary>
/// Fabrique utilisée par les outils EF Core (dotnet ef) au design-time pour générer
/// les migrations sans démarrer l'API. La chaîne de connexion n'a pas besoin d'être
/// joignable : seul le provider (Npgsql) est requis pour l'échafaudage.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MEDIAPLATFORM_DB")
            ?? "Host=localhost;Port=5432;Database=mediaplatform;Username=media;Password=media";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new AppDbContext(options);
    }
}
