namespace MediaPlatform.Domain.Entities;

/// <summary>Compte utilisateur (Admin, Éditeur ou Visiteur via les rôles).</summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
}

/// <summary>Rôle applicatif (RBAC).</summary>
public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // Admin | Editeur | Visiteur
    public ICollection<UserRole> Users { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}
