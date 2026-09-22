using System.Diagnostics;

namespace Intropy.Telemetry.Test;

public class StrictSourceFilterProcessorTests
{
    /// <summary>
    /// Runs a span from <paramref name="sourceName" /> through the processor and reports whether it
    /// survived, i.e. whether it is still flagged for export.
    /// </summary>
    private static bool Survives(string sourceName, params string[] allowedSources)
    {
        var processor = new StrictSourceFilterProcessor(allowedSources);

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("work");

        Assert.NotNull(activity);
        processor.OnEnd(activity);

        return activity.Recorded;
    }

    [Theory]
    [InlineData("Intropy.Storage")]
    [InlineData("Intropy.")]
    [InlineData("Intropy.Deeply.Nested.Source")]
    public void WildcardPattern_MatchesEverythingUnderThePrefix(string sourceName)
    {
        Assert.True(Survives(sourceName, "Intropy.*"));
    }

    [Theory]
    [InlineData("Intropy")]
    [InlineData("Intropyx")]
    [InlineData("NotIntropy.Storage")]
    [InlineData("System.Net.Http")]
    public void WildcardPattern_DoesNotMatchOutsideThePrefix(string sourceName)
    {
        Assert.False(Survives(sourceName, "Intropy.*"));
    }

    [Fact]
    public void ExactPattern_MatchesOnlyThatSource()
    {
        Assert.True(Survives("Orders.API", "Orders.API"));
        Assert.False(Survives("Orders.API.Internal", "Orders.API"));
    }

    [Theory]
    [InlineData("Orders.A", true)]
    [InlineData("Orders.1", true)]
    [InlineData("Orders.", false)]
    [InlineData("Orders.AB", false)]
    public void SingleCharacterWildcard_MatchesExactlyOneCharacter(string sourceName, bool expected)
    {
        Assert.Equal(expected, Survives(sourceName, "Orders.?"));
    }

    [Fact]
    public void Matching_IsCaseInsensitive()
    {
        Assert.True(Survives("INTROPY.Storage", "intropy.*"));
    }

    [Fact]
    public void RegexMetacharactersInAPattern_AreMatchedLiterally()
    {
        Assert.True(Survives("Orders.API", "Orders.API"));

        // The '.' must not stand in for an arbitrary character.
        Assert.False(Survives("OrdersXAPI", "Orders.API"));
    }

    [Fact]
    public void AnyMatchingPattern_IsEnoughToSurvive()
    {
        Assert.True(Survives("Orders.API", "Intropy.*", "Orders.API"));
    }

    [Fact]
    public void EmptyAllowlist_DropsEverything()
    {
        Assert.False(Survives("Intropy.Storage"));
    }
}
