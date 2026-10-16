using System.ComponentModel;
using System.Windows;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BexioOrderImport.Wpf.Views.Controls;

/// <summary>
/// Creates the preview of the active profile's format and shows the loaded order in it.
/// The host itself is invisible; the page places its <see cref="HeaderView"/> and <see cref="PositionsView"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class FormatPreviewHost : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(MappingProfile), typeof(FormatPreviewHost), new PropertyMetadata(null, OnProfileChanged));

    public static readonly DependencyProperty OrderProperty = DependencyProperty.Register(
        nameof(Order), typeof(Order), typeof(FormatPreviewHost), new PropertyMetadata(null, OnChanged));

    private static readonly DependencyPropertyKey HeaderViewPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HeaderView), typeof(FrameworkElement), typeof(FormatPreviewHost), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey PositionsViewPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PositionsView), typeof(FrameworkElement), typeof(FormatPreviewHost), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey IsShownPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsShown), typeof(bool), typeof(FormatPreviewHost), new PropertyMetadata(false));

    public static readonly DependencyProperty HeaderViewProperty = HeaderViewPropertyKey.DependencyProperty;

    public static readonly DependencyProperty PositionsViewProperty = PositionsViewPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsShownProperty = IsShownPropertyKey.DependencyProperty;

    private string? _shownFormatId;
    private IImportPreviewControl? _preview;

    public FormatPreviewHost()
    {
        Focusable = false;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Active profile. Its format and settings are read together, so the preview never gets the settings of another format.
    /// </summary>
    public MappingProfile? Profile
    {
        get => (MappingProfile?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    public Order? Order
    {
        get => (Order?)GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    /// <summary>
    /// Order header of the format's preview, null without a known format.
    /// </summary>
    public FrameworkElement? HeaderView => (FrameworkElement?)GetValue(HeaderViewProperty);

    /// <summary>
    /// Positions of the format's preview, null without a known format.
    /// </summary>
    public FrameworkElement? PositionsView => (FrameworkElement?)GetValue(PositionsViewProperty);

    /// <summary>
    /// True while an order is shown.
    /// </summary>
    public bool IsShown => (bool)GetValue(IsShownProperty);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FormatPreviewHost)d).Refresh();

    private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (FormatPreviewHost)d;
        if (e.OldValue is MappingProfile oldProfile)
        {
            oldProfile.PropertyChanged -= host.OnProfilePropertyChanged;
        }
        if (e.NewValue is MappingProfile newProfile)
        {
            newProfile.PropertyChanged += host.OnProfilePropertyChanged;
        }
        host.Refresh();
    }

    // Editing the active profile replaces its settings
    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MappingProfile.Settings))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        string? formatId = Profile?.FormatId;
        if (formatId != _shownFormatId)
        {
            _shownFormatId = formatId;
            var formatUis = App.Services?.GetService<FormatUiRegistry>();
            _preview = formatUis?.Find(formatId)?.CreateImportPreviewControl();
            SetValue(HeaderViewPropertyKey, _preview?.HeaderView);
            SetValue(PositionsViewPropertyKey, _preview?.PositionsView);
        }

        var settings = Profile?.Settings;
        if (_preview != null && Order != null && settings != null)
        {
            _preview.Show(Order, settings);
            SetValue(IsShownPropertyKey, true);
        }
        else
        {
            _preview?.Clear();
            SetValue(IsShownPropertyKey, false);
        }
    }
}
