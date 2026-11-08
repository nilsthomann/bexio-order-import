using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.ViewModels;

/// <summary>
/// Elapsed and remaining time of an import. The remaining time blends the upload speed since the first position
/// with the speed of the last seconds, is smoothed, so it does not jump with every position, and counts down between positions.
/// </summary>
public sealed class ImportTimeEstimator
{
    private readonly List<(DateTime Timestamp, double UploadedCount)> _samples = [];
    private (DateTime Timestamp, double UploadedCount)? _firstSample;
    private double _smoothedSecondsPerItem;
    private double _uploaded;
    private double _total;
    private double? _remainingSeconds;
    private TimeSpan _estimatedAt;

    public void Reset()
    {
        _samples.Clear();
        _firstSample = null;
        _smoothedSecondsPerItem = 0;
        _uploaded = 0;
        _total = 0;
        _remainingSeconds = null;
        _estimatedAt = TimeSpan.Zero;
    }

    /// <summary>
    /// Records the uploaded positions; samples older than 3 minutes are dropped.
    /// </summary>
    public void AddSample(double uploaded, DateTime now)
    {
        _firstSample ??= (now, uploaded);
        _samples.Add((now, uploaded));
        var cutoff = now.AddMinutes(-3);
        _samples.RemoveAll(s => s.Timestamp < cutoff);
    }

    /// <summary>
    /// Updates the estimate with the uploaded positions and returns the progress text.
    /// </summary>
    public string GetTimeText(double uploaded, double total, TimeSpan elapsed, DateTime now)
    {
        _uploaded = uploaded;
        _total = total;

        if (uploaded >= 3 && _samples.Count >= 2 && _firstSample is { } first && uploaded > first.UploadedCount)
        {
            // The work before the first position (article lookup, customer and order) is not upload speed
            double overallSecondsPerItem = (now - first.Timestamp).TotalSeconds / (uploaded - first.UploadedCount);

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

            _remainingSeconds = Math.Max(0, (total - uploaded) * _smoothedSecondsPerItem);
            _estimatedAt = elapsed;
        }

        return GetTimeText(elapsed);
    }

    /// <summary>
    /// The progress text with the elapsed time and, until all positions are uploaded, the last estimate counted down since it was made.
    /// </summary>
    public string GetTimeText(TimeSpan elapsed)
    {
        string elapsedStr = string.Format("{0:D2}:{1:D2}", (int)elapsed.TotalMinutes, elapsed.Seconds);

        if (_total > 0 && _uploaded >= _total)
        {
            return string.Format(Translations.Import_ProgressTimeElapsedOnly, elapsedStr);
        }

        string remainingStr = "-";
        if (_remainingSeconds is { } estimate)
        {
            double remainingSeconds = Math.Max(0, estimate - (elapsed - _estimatedAt).TotalSeconds);
            TimeSpan remainingTs = TimeSpan.FromSeconds(remainingSeconds);
            remainingStr = remainingTs.TotalMinutes < 1
                ? $"~{Math.Max(1, (int)Math.Ceiling(remainingSeconds))}s"
                : $"~{(int)remainingTs.TotalMinutes}m {remainingTs.Seconds}s";
        }

        return string.Format(Translations.Import_ProgressTime, elapsedStr, remainingStr);
    }
}
