using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class MotionTests
{
    [Fact]
    public void EaseOutStartsAtZeroEndsAtOneAndNeverGoesBack()
    {
        Assert.Equal(0f, Motion.EaseOut(0f));
        Assert.Equal(1f, Motion.EaseOut(1f));
        var previous = 0f;
        for (var step = 1; step <= 100; step++)
        {
            var value = Motion.EaseOut(step / 100f);
            Assert.True(value >= previous);
            previous = value;
        }
        // Decelera: la prima metà del tempo copre più di metà del cammino.
        Assert.True(Motion.EaseOut(0.5f) > 0.5f);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    public void EaseOutIsClampedOutsideTheRange(float input, float expected) =>
        Assert.Equal(expected, Motion.EaseOut(input));

    [Fact]
    public void ProgressIsClampedAndZeroDurationIsAlreadyFinished()
    {
        Assert.Equal(0f, Motion.Progress(-50, 200));
        Assert.Equal(0.5f, Motion.Progress(100, 200));
        Assert.Equal(1f, Motion.Progress(900, 200));
        Assert.Equal(1f, Motion.Progress(0, 0));
    }

    [Fact]
    public void TweenMovesToItsTargetInTheGivenTime()
    {
        var tween = new Tween(10f);
        tween.MoveTo(110f, nowMs: 1000, durationMs: 200);

        Assert.Equal(10f, tween.ValueAt(1000));
        Assert.True(tween.IsRunning(1100));
        var middle = tween.ValueAt(1100);
        Assert.InRange(middle, 60f, 110f);
        Assert.Equal(110f, tween.ValueAt(1200));
        Assert.False(tween.IsRunning(1200));
        Assert.Equal(110f, tween.ValueAt(5000));
    }

    [Fact]
    public void TweenRetargetedMidwayStartsFromWhereItIsWithoutJumping()
    {
        var tween = new Tween();
        tween.MoveTo(100f, 0, 200);
        var before = tween.ValueAt(80);

        tween.MoveTo(0f, 80, 200);

        Assert.Equal(before, tween.ValueAt(80), 3);
        Assert.True(tween.ValueAt(150) < before);
        Assert.Equal(0f, tween.ValueAt(280), 3);
    }

    [Fact]
    public void TweenWithZeroDurationJumpsAndJumpStopsIt()
    {
        var tween = new Tween(5f);
        tween.MoveTo(50f, 0, 0);
        Assert.Equal(50f, tween.ValueAt(0));
        Assert.False(tween.IsRunning(0));

        tween.MoveTo(80f, 0, 300);
        tween.Jump(7f);
        Assert.Equal(7f, tween.ValueAt(10));
        Assert.False(tween.IsRunning(10));
        Assert.Equal(7f, tween.Target);
    }
}
