using BookingsAssistant.Api.Services;

namespace BookingsAssistant.Tests.Services;

public class GateCodeStatusEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private const int DaysBefore = 2;

    private static string Evaluate(
        string status = "Confirmed",
        DateTime? startDate = null,
        DateTime? gateCodeSentAt = null,
        bool coveredByDuty = false)
        => GateCodeStatusEvaluator.Evaluate(
            status,
            startDate ?? Now.Date.AddDays(1),
            gateCodeSentAt,
            coveredByDuty,
            Now,
            DaysBefore);

    [Fact]
    public void WhenAlreadySent_ReturnsSent()
        => Assert.Equal(GateCodeStatusEvaluator.Sent,
            Evaluate(gateCodeSentAt: Now.AddDays(-1)));

    [Fact]
    public void WhenSentAndAlsoCoveredByDuty_PrioritisesSent()
        => Assert.Equal(GateCodeStatusEvaluator.Sent,
            Evaluate(gateCodeSentAt: Now.AddDays(-1), coveredByDuty: true));

    [Fact]
    public void WhenCoveredByDuty_ReturnsNotRequired()
        => Assert.Equal(GateCodeStatusEvaluator.NotRequired,
            Evaluate(coveredByDuty: true));

    [Fact]
    public void WhenNotConfirmed_ReturnsAwaitingConfirmation()
        => Assert.Equal(GateCodeStatusEvaluator.AwaitingConfirmation,
            Evaluate(status: "Provisional"));

    [Fact]
    public void WhenArrivalInPast_ReturnsArrivalPassed()
        => Assert.Equal(GateCodeStatusEvaluator.ArrivalPassed,
            Evaluate(startDate: Now.Date.AddDays(-1)));

    [Fact]
    public void WhenBeyondSendWindow_ReturnsScheduled()
        => Assert.Equal(GateCodeStatusEvaluator.Scheduled,
            Evaluate(startDate: Now.Date.AddDays(5)));

    [Fact]
    public void WhenWithinWindowAndConfirmed_ReturnsPending()
        => Assert.Equal(GateCodeStatusEvaluator.Pending,
            Evaluate(startDate: Now.Date.AddDays(1)));

    [Fact]
    public void WhenArrivingToday_ReturnsPending()
        => Assert.Equal(GateCodeStatusEvaluator.Pending,
            Evaluate(startDate: Now.Date));

    [Fact]
    public void StatusComparisonIsCaseInsensitive()
        => Assert.Equal(GateCodeStatusEvaluator.Pending,
            Evaluate(status: "confirmed"));
}
