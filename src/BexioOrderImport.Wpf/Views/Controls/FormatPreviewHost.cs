using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;
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
    public static readonly DependencyProperty FormatIdProperty = DependencyProperty.Register(
        nameof(FormatId), typeof(string), typeof(FormatPreviewHost), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings), typeof(IFormatSettings), typeof(FormatPreviewHost), new PropertyMetadata(null, OnChanged));

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

    public string? FormatId
    {
        get => (string?)GetValue(FormatIdProperty);
        set => SetValue(FormatIdProperty, value);
    }

    public IFormatSettings? Settings
    {
        get => (IFormatSettings?)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
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

    private void Refresh()
    {
        if (FormatId != _shownFormatId)
        {
            _shownFormatId = FormatId;
            var formatUis = App.Services?.GetService<FormatUiRegistry>();
            _preview = formatUis?.Find(FormatId)?.CreateImportPreviewControl();
            SetValue(HeaderViewPropertyKey, _preview?.HeaderView);
            SetValue(PositionsViewPropertyKey, _preview?.PositionsView);
        }

        if (_preview != null && Order != null && Settings != null)
        {
            _preview.Show(Order, Settings);
            SetValue(IsShownPropertyKey, true);
        }
        else
        {
            _preview?.Clear();
            SetValue(IsShownPropertyKey, false);
        }
    }
}
