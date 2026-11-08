using BexioOrderImport.Wpf.Resources;
using BexioOrderImport.Wpf.ViewModels;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class ImportTimeEstimatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 1 position after 0 s, 3 after 4 s and 5 after 10 s: 2.5 s per position since the first one, 3 s since the sample of 4 s ago.
    /// </summary>
    private static ImportTimeEstimator EstimatorWithSamples()
    {
        var estimator = new ImportTimeEstimator();
        estimator.AddSample(1, Start);
        estimator.AddSample(3, Start.AddSeconds(4));
        estimator.AddSample(5, Start.AddSeconds(10));
        return estimator;
    }

    [Test]
    public void GetTimeText_WithFewPositions_ShouldShowTheElapsedTimeWithoutEstimate()
    {
        var estimator = new ImportTimeEstimator();
        estimator.AddSample(1, Start);

        string text = estimator.GetTimeText(2, 10, TimeSpan.FromSeconds(65), Start.AddSeconds(65));

        text.Should().Be(string.Format(Translations.Import_ProgressTime, "01:05", "-"));
    }

    [Test]
    public void GetTimeText_WhenAllPositionsAreUploaded_ShouldShowTheElapsedTimeOnly()
    {
        string text = EstimatorWithSamples().GetTimeText(10, 10, TimeSpan.FromSeconds(65), Start.AddSeconds(65));

        text.Should().Be(string.Format(Translations.Import_ProgressTimeElapsedOnly, "01:05"));
    }

    [Test]
    [Arguments(10, "~14s")]
    [Arguments(102, "~4m 31s")]
    public void GetTimeText_ShouldBlendOverallAndRecentSpeed(int total, string remaining)
    {
        // 0.4 * 2.5 s + 0.6 * 3 s = 2.8 s for each remaining position
        string text = EstimatorWithSamples().GetTimeText(5, total, TimeSpan.FromSeconds(10), Start.AddSeconds(10));

        text.Should().Be(string.Format(Translations.Import_ProgressTime, "00:10", remaining));
    }

    [Test]
    public void GetTimeText_ShouldSmoothChangesOfTheSpeed()
    {
        var estimator = EstimatorWithSamples();
        estimator.GetTimeText(5, 100, TimeSpan.FromSeconds(10), Start.AddSeconds(10));
        estimator.AddSample(6, Start.AddSeconds(40));

        // The new target of 0.4 * 8 s + 0.6 * 30 s = 21.2 s only moves 15% from 2.8 s, to 5.56 s
        string text = estimator.GetTimeText(6, 100, TimeSpan.FromSeconds(40), Start.AddSeconds(40));

        text.Should().Be(string.Format(Translations.Import_ProgressTime, "00:40", "~8m 42s"));
    }

    [Test]
    public void GetTimeText_BetweenPositions_ShouldCountTheEstimateDown()
    {
        var estimator = EstimatorWithSamples();
        estimator.GetTimeText(5, 10, TimeSpan.FromSeconds(10), Start.AddSeconds(10));

        estimator.GetTimeText(TimeSpan.FromSeconds(15))
            .Should().Be(string.Format(Translations.Import_ProgressTime, "00:15", "~9s"));
    }

    [Test]
    public void GetTimeText_AfterTheLastPosition_ShouldKeepShowingTheElapsedTimeOnly()
    {
        var estimator = EstimatorWithSamples();
        estimator.GetTimeText(10, 10, TimeSpan.FromSeconds(10), Start.AddSeconds(10));

        estimator.GetTimeText(TimeSpan.FromSeconds(12))
            .Should().Be(string.Format(Translations.Import_ProgressTimeElapsedOnly, "00:12"));
    }

    [Test]
    public void Reset_ShouldForgetTheSamplesAndTheEstimate()
    {
        var estimator = EstimatorWithSamples();
        estimator.GetTimeText(5, 10, TimeSpan.FromSeconds(10), Start.AddSeconds(10));

        estimator.Reset();

        estimator.GetTimeText(5, 10, TimeSpan.FromSeconds(10), Start.AddSeconds(10))
            .Should().Be(string.Format(Translations.Import_ProgressTime, "00:10", "-"));
    }

    [Test]
    public void AddSample_ShouldDropSamplesOlderThanThreeMinutes()
    {
        var estimator = new ImportTimeEstimator();
        estimator.AddSample(1, Start);
        estimator.AddSample(3, Start.AddMinutes(4));

        // Only one sample is left, too few for an estimate
        estimator.GetTimeText(5, 10, TimeSpan.FromMinutes(4), Start.AddMinutes(4))
            .Should().Be(string.Format(Translations.Import_ProgressTime, "04:00", "-"));
    }
}
