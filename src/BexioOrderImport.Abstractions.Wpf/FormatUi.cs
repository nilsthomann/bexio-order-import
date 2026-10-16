using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Abstractions.Wpf;

/// <summary>
/// Creates the WPF controls of an import format. Registered next to its <see cref="IFormatAdapter"/>.
/// </summary>
public interface IFormatUiProvider
{
    /// <summary>
    /// <see cref="IFormatAdapter.FormatId"/> of the format.
    /// </summary>
    string FormatId { get; }

    IProfileConfigControl CreateProfileConfigControl();

    IImportPreviewControl CreateImportPreviewControl();
}

/// <summary>
/// Format-specific part of the profile editor. The host owns the profile name and format, and persists the settings.
/// </summary>
public interface IProfileConfigControl
{
    FrameworkElement View { get; }

    /// <summary>
    /// Shows the settings. The host passes a copy, so the control may change it freely.
    /// </summary>
    void Load(IFormatSettings settings);

    /// <summary>
    /// Localized messages of invalid inputs, empty when the inputs are valid.
    /// </summary>
    IReadOnlyList<string> Validate();

    /// <summary>
    /// Returns the edited settings. Only called after <see cref="Validate"/> returned no messages.
    /// </summary>
    IFormatSettings Save();
}

/// <summary>
/// Format-specific preview of a parsed order on the import page.
/// </summary>
public interface IImportPreviewControl
{
    /// <summary>
    /// Order header, shown across the full width above the positions and the import log.
    /// </summary>
    FrameworkElement HeaderView { get; }

    /// <summary>
    /// Positions and totals, shown next to the import log. It gets the remaining height of the page.
    /// </summary>
    FrameworkElement PositionsView { get; }

    void Show(Order order, IFormatSettings settings);

    void Clear();
}
