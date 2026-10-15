using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class StatusModelsTests
{
    private static readonly DateTime Now = new(2026, 10, 15);

    [Fact]
    public void NoSignatureIsNeutral()
    {
        var (text, level) = CertificateStatus.Describe(null, Now);
        Assert.Equal("non firmato", text);
        Assert.Equal(HealthLevel.Neutral, level);
    }

    [Fact]
    public void FarExpiryIsGoodWithMonthAndYear()
    {
        var (text, level) = CertificateStatus.Describe(new DateTime(2030, 3, 20), Now);
        Assert.Equal("valido fino a 03/2030", text);
        Assert.Equal(HealthLevel.Good, level);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [InlineData(0, true)]
    public void WarningStartsSixtyDaysBefore(int days, bool warning) =>
        Assert.Equal(
            warning ? HealthLevel.Warning : HealthLevel.Good,
            CertificateStatus.Describe(Now.AddDays(days).AddHours(1), Now).Level);

    [Fact]
    public void WarningCountsDays()
    {
        var (text, _) = CertificateStatus.Describe(Now.AddDays(12).AddHours(2), Now);
        Assert.Equal("scade fra 12 giorni", text);
    }

    [Fact]
    public void ExpiredIsBad()
    {
        var (text, level) = CertificateStatus.Describe(Now.AddDays(-1), Now);
        Assert.Equal("scaduto", text);
        Assert.Equal(HealthLevel.Bad, level);
    }

    [Fact]
    public void SpinnerTurnsOnceEveryRevolution()
    {
        Assert.Equal(0f, SpinnerMath.StartAngle(0), 3);
        Assert.Equal(180f, SpinnerMath.StartAngle(SpinnerMath.RevolutionMs / 2), 2);
        Assert.Equal(0f, SpinnerMath.StartAngle(SpinnerMath.RevolutionMs * 3), 2);
        Assert.InRange(SpinnerMath.StartAngle(-100), 0f, 360f);
    }

    [Fact]
    public void SpinnerSweepStaysBetweenBounds()
    {
        for (var ms = 0; ms < 5000; ms += 37)
        {
            Assert.InRange(SpinnerMath.Sweep(ms), SpinnerMath.MinSweep - 0.01f, SpinnerMath.MaxSweep + 0.01f);
        }
        Assert.Equal(SpinnerMath.MinSweep, SpinnerMath.Sweep(0), 2);
        Assert.Equal(SpinnerMath.MaxSweep, SpinnerMath.Sweep(SpinnerMath.RevolutionMs), 2);
    }
}
