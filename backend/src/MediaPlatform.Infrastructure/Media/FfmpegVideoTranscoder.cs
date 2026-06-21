using System.Diagnostics;
using System.Globalization;
using MediaPlatform.Application.Interfaces;

namespace MediaPlatform.Infrastructure.Media;

/// <summary>Transcodeur basé sur les binaires ffmpeg/ffprobe (présents dans l'image worker).</summary>
public class FfmpegVideoTranscoder : IVideoTranscoder
{
    public async Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
    {
        var heightRaw = await RunAsync("ffprobe",
            $"-v error -select_streams v:0 -show_entries stream=height -of csv=p=0 \"{sourcePath}\"", ct);
        var durationRaw = await RunAsync("ffprobe",
            $"-v error -show_entries format=duration -of csv=p=0 \"{sourcePath}\"", ct);

        if (string.IsNullOrWhiteSpace(heightRaw))
            throw new InvalidOperationException($"ffprobe n'a renvoyé aucune hauteur pour « {sourcePath} » (flux vidéo absent ?).");

        return (int.Parse(heightRaw.Trim(), CultureInfo.InvariantCulture),
                double.Parse(durationRaw.Trim(), CultureInfo.InvariantCulture));
    }

    public async Task<string> TranscodeRungAsync(string sourcePath, string outDir, int height, int videoKbps,
        int audioKbps, int segmentSeconds, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);
        var playlist = "index.m3u8";
        var args =
            $"-y -i \"{sourcePath}\" -vf scale=-2:{height} -c:v libx264 -profile:v main -preset veryfast " +
            $"-b:v {videoKbps}k -maxrate {(int)(videoKbps * 1.07)}k -bufsize {videoKbps * 2}k " +
            $"-c:a aac -b:a {audioKbps}k -hls_time {segmentSeconds} -hls_playlist_type vod " +
            $"-hls_segment_filename \"{Path.Combine(outDir, "seg_%03d.ts")}\" \"{Path.Combine(outDir, playlist)}\"";
        await RunAsync("ffmpeg", args, ct);
        return playlist;
    }

    private static async Task<string> RunAsync(string file, string args, CancellationToken ct)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        p.Start();
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{file} a échoué (code {p.ExitCode}) : {await stderrTask}");
        return await stdoutTask;
    }
}
