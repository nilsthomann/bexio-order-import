using System;
using System.Text.Json;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Wpf.Models;

namespace BexioOrderImport.Wpf.Services;

/// <summary>
/// Converts profiles from and to their stored form in appsettings.json and profile exports.
/// </summary>
public class ProfileSerializer
{
    private readonly IFormatRegistry _registry;

    public ProfileSerializer(IFormatRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public MappingProfile FromDto(MappingProfileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Profiles saved before formats existed are Peakmile profiles with their settings in "ExcelMapping"
        string formatId = dto.Format ?? PeakmileFormatAdapter.Id;
        JsonElement? json = dto.Settings ?? dto.ExcelMapping;
        var adapter = _registry.Find(formatId);

        IFormatSettings? settings = null;
        if (adapter != null)
        {
            settings = json is { ValueKind: JsonValueKind.Object } element
                ? adapter.DeserializeSettings(element)
                : adapter.CreateDefaultSettings();
        }

        return new MappingProfile
        {
            Name = dto.Name,
            FormatId = adapter?.FormatId ?? formatId,
            Settings = settings,
            RawSettings = json
        };
    }

    public MappingProfileDto ToDto(MappingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var adapter = _registry.Find(profile.FormatId);
        return new MappingProfileDto
        {
            Name = profile.Name,
            Format = profile.FormatId,
            Settings = adapter != null && profile.Settings != null
                ? adapter.SerializeSettings(profile.Settings)
                : profile.RawSettings
        };
    }
}
