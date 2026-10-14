using System.Text.Json;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Wpf.ViewModels;

namespace BexioOrderImport.Wpf.Models;

public class MappingProfile : ViewModelBase
{
    private string _name = string.Empty;
    private IFormatSettings? _settings;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// <see cref="IFormatAdapter.FormatId"/> of the profile. Chosen on creation and never changed.
    /// </summary>
    public required string FormatId { get; init; }

    /// <summary>
    /// Null if the format is not registered (eg. a profile from a newer version), see <see cref="RawSettings"/>.
    /// </summary>
    public IFormatSettings? Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    /// <summary>
    /// Settings JSON as loaded, written back unchanged while the format is not registered.
    /// </summary>
    internal JsonElement? RawSettings { get; init; }
}
