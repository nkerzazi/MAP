namespace MediaPlatform.Infrastructure.Persistence;

/// <summary>Identifiants fixes des données de référence (seed déterministe via migrations).</summary>
public static class SeedData
{
    public static readonly Guid AdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EditeurRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid VisiteurRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
}
