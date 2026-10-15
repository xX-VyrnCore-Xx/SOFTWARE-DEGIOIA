using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class UpdateProgressModelTests
{
    [Fact]
    public void StepsAreInOrderWithLabels()
    {
        Assert.Equal(
            ["Scaricamento", "Controllo del file", "Verifica della firma", "Installazione"],
            UpdateSteps.All.Select(UpdateSteps.Label).ToArray());
    }

    [Fact]
    public void StepStateFollowsTheCurrentStep()
    {
        Assert.Equal(StepState.Done, UpdateSteps.StateOf(UpdateStep.Download, UpdateStep.Signature));
        Assert.Equal(StepState.Done, UpdateSteps.StateOf(UpdateStep.Checksum, UpdateStep.Signature));
        Assert.Equal(StepState.Active, UpdateSteps.StateOf(UpdateStep.Signature, UpdateStep.Signature));
        Assert.Equal(StepState.Pending, UpdateSteps.StateOf(UpdateStep.Install, UpdateStep.Signature));
        Assert.Equal(StepState.Failed, UpdateSteps.StateOf(UpdateStep.Signature, UpdateStep.Signature, failed: true));
    }

    [Theory]
    [InlineData(0L, "0,0 MB")]
    [InlineData(52428800L, "50,0 MB")]
    [InlineData(1572864L, "1,5 MB")]
    [InlineData(51200L, "50,0 KB")]
    public void SizesUseTheItalianComma(long bytes, string expected) =>
        Assert.Equal(expected, TransferMeter.FormatSize(bytes));

    [Theory]
    [InlineData(5.0, "circa 5 s")]
    [InlineData(0.2, "circa 1 s")]
    [InlineData(59.0, "circa 59 s")]
    [InlineData(65.0, "circa 1 min 05 s")]
    [InlineData(600.0, "circa 10 min 00 s")]
    public void RemainingTimeIsReadable(double seconds, string expected) =>
        Assert.Equal(expected, TransferMeter.FormatRemaining(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void UnknownRemainingTimeIsEmpty() => Assert.Equal(string.Empty, TransferMeter.FormatRemaining(null));

    [Fact]
    public void FirstSampleGivesAverageSpeedAndEta()
    {
        var meter = new TransferMeter();
        meter.Add(10 * 1048576, 50 * 1048576, TimeSpan.FromSeconds(2));
        Assert.Equal(5 * 1048576, meter.BytesPerSecond, 1);
        Assert.Equal(20, meter.Percent);
        Assert.Equal(8, meter.Remaining!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void SpeedIsSmoothedNotJumpy()
    {
        var meter = new TransferMeter();
        meter.Add(10 * 1048576, 100 * 1048576, TimeSpan.FromSeconds(1)); // 10 MB/s
        meter.Add(10 * 1048576 + 1048576, 100 * 1048576, TimeSpan.FromSeconds(2)); // un secondo a 1 MB/s
        Assert.InRange(meter.BytesPerSecond, 6.5 * 1048576, 7.5 * 1048576);
    }

    [Fact]
    public void TooCloseSamplesDoNotChangeTheSpeed()
    {
        var meter = new TransferMeter();
        meter.Add(1048576, 10 * 1048576, TimeSpan.FromSeconds(1));
        var before = meter.BytesPerSecond;
        meter.Add(2 * 1048576, 10 * 1048576, TimeSpan.FromSeconds(1.1));
        Assert.Equal(before, meter.BytesPerSecond);
        Assert.Equal(2 * 1048576, meter.Received);
    }

    [Fact]
    public void NoTotalMeansNoPercentNoRemaining()
    {
        var meter = new TransferMeter();
        meter.Add(1000, 0, TimeSpan.FromSeconds(1));
        Assert.Equal(0, meter.Percent);
        Assert.Null(meter.Remaining);
    }

    [Fact]
    public void DescribeOmitsUnknownParts()
    {
        var meter = new TransferMeter();
        Assert.Equal("0,0 MB di 0,0 MB", meter.Describe());
        meter.Add(10 * 1048576, 50 * 1048576, TimeSpan.FromSeconds(2));
        Assert.Equal("10,0 MB di 50,0 MB · 5,0 MB/s · circa 8 s", meter.Describe());
    }
}
