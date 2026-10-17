using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.ViewModels;

/// <summary>
/// Elapsed and remaining time of an import. The remaining time blends the overall speed with the speed
/// of the last seconds and is smoothed, so it does not jump with every position.
/// </summary>
public sealed class ImportTimeEstimator
{
    private readonly List<(DateTime Timestamp, double UploadedCount)> _samples = [];
    private double _smoothedSecondsPerItem;
    private string _lastFormattedRemaining = "-";

    public void Reset()
    {
        _samples.Clear();
        _smoothedSecondsPerItem = 0;
        _lastFormattedRemaining = "-";
    }

    /// <summary>
    /// Records the uploaded positions; samples older than 3 minutes are dropped.
    /// </summary>
    public void AddSample(double uploaded, DateTime now)
    {
        _samples.Add((now, uploaded));
        var cutoff = now.AddMinutes(-3);
        _samples.RemoveAll(s => s.Timestamp < cutoff);
    }

    /// <summary>
    /// The progress text with the elapsed time and, until all positions are uploaded, the estimated remaining time.
    /// </summary>
    public string GetTimeText(double uploaded, double total, TimeSpan elapsed, DateTime now)
    {
        string elapsedStr = string.Format("{0:D2}:{1:D2}", (int)elapsed.TotalMinutes, elapsed.Seconds);

        if (total > 0 && uploaded >= total)
        {
            return string.Format(Translations.Import_ProgressTimeElapsedOnly, elapsedStr);
        }

        if (uploaded >= 3 && _samples.Count >= 2)
        {
            // Calculate overall average seconds per item
            double overallSecondsPerItem = elapsed.TotalSeconds / Math.Max(1, uploaded);

            // Find a reference sample from 4+ seconds ago for rolling velocity
            var validSamples = _samples
                .Where(s => (now - s.Timestamp).TotalSeconds >= 4 && s.UploadedCount < uploaded)
                .ToList();

            double currentInstantSecondsPerItem = overallSecondsPerItem;
            if (validSamples.Count > 0)
            {
                var refSample = validSamples[^1];
                double dItems = uploaded - refSample.UploadedCount;
                double dSec = (now - refSample.Timestamp).TotalSeconds;
                if (dItems > 0 && dSec > 0)
                {
                    currentInstantSecondsPerItem = dSec / dItems;
                }
            }

            // Blend overall average (40%) and recent velocity (60%) for stability
            double targetSecondsPerItem = (overallSecondsPerItem * 0.4) + (currentInstantSecondsPerItem * 0.6);

            // Apply Exponential Moving Average (EMA) smoothing to prevent UI flickering
            _smoothedSecondsPerItem = _smoothedSecondsPerItem <= 0
                ? targetSecondsPerItem
                : (_smoothedSecondsPerItem * 0.85) + (targetSecondsPerItem * 0.15);

            double remainingSeconds = Math.Max(0, (total - uploaded) * _smoothedSecondsPerItem);
            TimeSpan remainingTs = TimeSpan.FromSeconds(remainingSeconds);
            _lastFormattedRemaining = remainingTs.TotalMinutes < 1
                ? $"~{Math.Max(1, (int)Math.Ceiling(remainingSeconds))}s"
                : $"~{(int)remainingTs.TotalMinutes}m {remainingTs.Seconds}s";
        }

        return string.Format(Translations.Import_ProgressTime, elapsedStr, _lastFormattedRemaining);
    }
}
