using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

public class OsmRateLimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RetryAfter_DeltaSeconds_IsHonoured()
        => Assert.Equal(TimeSpan.FromSeconds(30),
            OsmRateLimit.GetRetryAfterDelay("30", null, Now));

    [Fact]
    public void RetryAfter_HttpDate_IsConvertedToDelay()
    {
        var when = Now.AddSeconds(45).ToString("R"); // RFC1123 HTTP-date
        var delay = OsmRateLimit.GetRetryAfterDelay(when, null, Now);
        Assert.Equal(45, delay.TotalSeconds, precision: 0);
    }

    [Fact]
    public void RetryAfter_TakesPriorityOverReset()
        => Assert.Equal(TimeSpan.FromSeconds(20),
            OsmRateLimit.GetRetryAfterDelay("20", "90", Now));

    [Fact]
    public void RetryAfter_FallsBackToReset_WhenHeaderMissing()
        => Assert.Equal(TimeSpan.FromSeconds(45),
            OsmRateLimit.GetRetryAfterDelay(null, "45", Now));

    [Fact]
    public void Reset_AsUnixTimestamp_IsConvertedToDelay()
    {
        var epoch = Now.AddSeconds(60).ToUnixTimeSeconds().ToString();
        var delay = OsmRateLimit.GetRetryAfterDelay(null, epoch, Now);
        Assert.Equal(60, delay.TotalSeconds, precision: 0);
    }

    [Fact]
    public void RetryAfter_WithNoHints_UsesFallback()
        => Assert.Equal(OsmRateLimit.FallbackBackoff,
            OsmRateLimit.GetRetryAfterDelay(null, null, Now));

    [Fact]
    public void RetryAfter_NegativeOrZero_UsesFallback()
        => Assert.Equal(OsmRateLimit.FallbackBackoff,
            OsmRateLimit.GetRetryAfterDelay("0", null, Now));

    [Fact]
    public void RetryAfter_IsCappedAtMax()
        => Assert.Equal(OsmRateLimit.MaxBackoff,
            OsmRateLimit.GetRetryAfterDelay("9999", null, Now));

    [Fact]
    public void Proactive_NoDelay_WhenRemainingHasHeadroom()
        => Assert.Null(OsmRateLimit.GetProactiveDelay("50", "60", Now));

    [Fact]
    public void Proactive_PausesUntilReset_WhenRemainingLow()
        => Assert.Equal(TimeSpan.FromSeconds(60),
            OsmRateLimit.GetProactiveDelay("3", "60", Now));

    [Fact]
    public void Proactive_AtThreshold_Pauses()
        => Assert.Equal(TimeSpan.FromSeconds(30),
            OsmRateLimit.GetProactiveDelay(OsmRateLimit.LowRemainingThreshold.ToString(), "30", Now));

    [Fact]
    public void Proactive_NoDelay_WhenRemainingLowButNoReset()
        => Assert.Null(OsmRateLimit.GetProactiveDelay("1", null, Now));

    [Fact]
    public void Proactive_IsCappedAtMax()
        => Assert.Equal(OsmRateLimit.MaxBackoff,
            OsmRateLimit.GetProactiveDelay("0", "9999", Now));
}
