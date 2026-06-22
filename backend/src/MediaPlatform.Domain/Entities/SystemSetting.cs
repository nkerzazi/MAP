namespace MediaPlatform.Domain.Entities;

/// <summary>Paramètre de configuration système (clé/valeur), modifiable par un Admin.</summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
