namespace MediaPlatform.Application.Media;

/// <summary>Un échelon de la ladder HLS (résolution cible et débits associés).</summary>
public class LadderRung
{
    public string Name { get; set; } = string.Empty;
    public int Height { get; set; }
    public int VideoKbps { get; set; }
    public int AudioKbps { get; set; }
}
