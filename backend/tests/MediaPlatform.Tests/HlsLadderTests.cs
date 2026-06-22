using FluentAssertions;
using MediaPlatform.Application.Media;
using Xunit;

namespace MediaPlatform.Tests;

public class HlsLadderTests
{
    private static readonly TranscodingOptions Opts = new();

    [Fact]
    public void Source_1080p_or_more_selects_all_three_rungs()
    {
        var rungs = HlsLadder.Select(sourceHeight: 2160, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p", "720p", "1080p");
    }

    [Fact]
    public void Source_480p_selects_only_360p()
    {
        var rungs = HlsLadder.Select(sourceHeight: 480, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p");
    }

    [Fact]
    public void Source_below_360p_still_selects_lowest_rung()
    {
        var rungs = HlsLadder.Select(sourceHeight: 240, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p");
    }

    [Fact]
    public void Source_720p_selects_360_and_720()
    {
        var rungs = HlsLadder.Select(sourceHeight: 720, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p", "720p");
    }
}
