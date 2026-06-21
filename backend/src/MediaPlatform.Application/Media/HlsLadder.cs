namespace MediaPlatform.Application.Media;

/// <summary>Sélection adaptative des échelons HLS : on ne transcode jamais au-dessus
/// de la résolution source (pas d'upscaling), mais on garde toujours au moins le plus bas.</summary>
public static class HlsLadder
{
    public static IReadOnlyList<LadderRung> Select(int sourceHeight, TranscodingOptions options)
    {
        var ordered = options.Rungs.OrderBy(r => r.Height).ToList();
        var applicable = ordered.Where(r => r.Height <= sourceHeight).ToList();
        return applicable.Count > 0 ? applicable : new List<LadderRung> { ordered.First() };
    }
}
