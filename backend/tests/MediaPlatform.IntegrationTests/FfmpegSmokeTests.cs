using System.Diagnostics;
using FluentAssertions;
using MediaPlatform.Infrastructure.Media;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class FfmpegSmokeTests
{
    private static bool FfmpegAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
                { RedirectStandardOutput = true, UseShellExecute = false });
            p!.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    [SkippableFact]
    public async Task Generates_hls_playlist_and_segments_from_a_tiny_clip()
    {
        Skip.IfNot(FfmpegAvailable(), "ffmpeg non installé sur ce runner");

        var work = Directory.CreateTempSubdirectory("ffmpeg-smoke-");
        var source = Path.Combine(work.FullName, "src.mp4");
        // Génère un clip 2s 640x480 de mire.
        using (var gen = Process.Start(new ProcessStartInfo("ffmpeg",
            $"-y -f lavfi -i testsrc=duration=2:size=640x480:rate=15 -pix_fmt yuv420p \"{source}\"")
            { UseShellExecute = false }))
        { gen!.WaitForExit(); }

        var t = new FfmpegVideoTranscoder();
        var (height, duration) = await t.ProbeAsync(source);
        height.Should().Be(480);
        duration.Should().BeApproximately(2, 0.5);

        var outDir = Path.Combine(work.FullName, "360p");
        await t.TranscodeRungAsync(source, outDir, 360, 800, 96, 6);

        File.Exists(Path.Combine(outDir, "index.m3u8")).Should().BeTrue();
        Directory.EnumerateFiles(outDir, "*.ts").Should().NotBeEmpty();

        work.Delete(recursive: true);
    }
}
