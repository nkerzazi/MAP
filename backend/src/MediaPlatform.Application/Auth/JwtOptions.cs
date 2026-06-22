namespace MediaPlatform.Application.Auth;

/// <summary>Paramètres JWT (section "Jwt" de la configuration).</summary>
public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "map";
    public string Audience { get; set; } = "map";
    public int ExpiryHours { get; set; } = 8;
}
