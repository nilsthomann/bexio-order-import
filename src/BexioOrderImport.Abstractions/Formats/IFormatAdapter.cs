using System.Text.Json;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Abstractions.Formats;

/// <summary>
/// An import format: parses its Excel files and imports the parsed order into Bexio.
/// </summary>
public interface IFormatAdapter
{
    /// <summary>
    /// Stable id stored in the profiles, eg. "peakmile". Never localized or renamed.
    /// </summary>
    string FormatId { get; }

    string DisplayName { get; }

    string Description { get; }

    Version Version { get; }

    IFormatSettings CreateDefaultSettings();

    IFormatSettings DeserializeSettings(JsonElement json);

    JsonElement SerializeSettings(IFormatSettings settings);

    IFormatSettings CloneSettings(IFormatSettings settings);

    Order Parse(string filePath, IFormatSettings settings);

    Task<ImportResult> ImportAsync(
        Order order,
        IFormatSettings settings,
        IBexioService bexio,
        IImportUserInteractionService interaction);
}
