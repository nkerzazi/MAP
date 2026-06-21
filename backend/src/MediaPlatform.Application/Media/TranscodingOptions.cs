namespace MediaPlatform.Application.Media;

/// <summary>Paramètres du pipeline de transcodage (section "Transcoding").</summary>
public class TranscodingOptions
{
    public int SegmentSeconds { get; set; } = 6;
    public int MaxAttempts { get; set; } = 3;
    public List<LadderRung> Rungs { get; set; } = new()
    {
        new LadderRung { Name = "360p",  Height = 360,  VideoKbps = 800,  AudioKbps = 96 },
        new LadderRung { Name = "720p",  Height = 720,  VideoKbps = 2500, AudioKbps = 128 },
        new LadderRung { Name = "1080p", Height = 1080, VideoKbps = 5000, AudioKbps = 128 },
    };
}

public class LadderRung
{
    public string Name { get; set; } = string.Empty;
    public int Height { get; set; }
    public int VideoKbps { get; set; }
    public int AudioKbps { get; set; }
}
