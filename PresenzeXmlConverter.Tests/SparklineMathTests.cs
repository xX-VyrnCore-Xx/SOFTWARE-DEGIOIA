namespace PresenzeXmlConverter.Tests;

public class SparklineMathTests
{
    [Fact]
    public void ValuesAreScaledBetweenZeroAndOne()
    {
        Assert.Equal(new[] { 0f, 0.5f, 1f }, SparklineMath.Normalize(new[] { 10f, 20f, 30f }));
        Assert.Equal(new[] { 1f, 0f, 0.5f }, SparklineMath.Normalize(new[] { 30f, 10f, 20f }));
    }

    [Fact]
    public void AFlatSeriesSitsInTheMiddle() =>
        Assert.Equal(new[] { 0.5f, 0.5f, 0.5f }, SparklineMath.Normalize(new[] { 7f, 7f, 7f }));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FewerThanTwoValuesDrawNothing(int count) =>
        Assert.Empty(SparklineMath.Normalize(new float[count].Select(_ => 5f).ToArray()));
}
