using System.Text.Json;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Abstractions.Formats;

/// <summary>
/// Base class for formats with a typed settings class. Handles casting, JSON and cloning.
/// </summary>
public abstract class FormatAdapter<TSettings> : IFormatAdapter
    where TSettings : class, IFormatSettings, new()
{
    public abstract string FormatId { get; }

    public abstract string DisplayName { get; }

    public abstract string Description { get; }

    public Version Version => GetType().Assembly.GetName().Version ?? new Version(0, 0);

    public IFormatSettings CreateDefaultSettings() => new TSettings();

    public IFormatSettings DeserializeSettings(JsonElement json) => json.Deserialize<TSettings>() ?? new TSettings();

    public JsonElement SerializeSettings(IFormatSettings settings) => JsonSerializer.SerializeToElement(Cast(settings));

    public IFormatSettings CloneSettings(IFormatSettings settings) => DeserializeSettings(SerializeSettings(settings));

    public Order Parse(string filePath, IFormatSettings settings) => Parse(filePath, Cast(settings));

    public Task<ImportResult> ImportAsync(Order order, IFormatSettings settings, IBexioService bexio, IImportUserInteractionService interaction)
        => ImportAsync(order, Cast(settings), bexio, interaction);

    protected abstract Order Parse(string filePath, TSettings settings);

    protected abstract Task<ImportResult> ImportAsync(Order order, TSettings settings, IBexioService bexio, IImportUserInteractionService interaction);

    private TSettings Cast(IFormatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings as TSettings
            ?? throw new ArgumentException($"Format '{FormatId}' expects {typeof(TSettings).Name}, got {settings.GetType().Name}.", nameof(settings));
    }
}
