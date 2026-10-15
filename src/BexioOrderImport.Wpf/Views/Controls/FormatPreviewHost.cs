using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BexioOrderImport.Wpf.Views.Controls;

/// <summary>
/// Shows the loaded order with the preview control of the active profile's format.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class FormatPreviewHost : ContentControl
{
    public static readonly DependencyProperty FormatIdProperty = DependencyProperty.Register(
        nameof(FormatId), typeof(string), typeof(FormatPreviewHost), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings), typeof(IFormatSettings), typeof(FormatPreviewHost), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty OrderProperty = DependencyProperty.Register(
        nameof(Order), typeof(Order), typeof(FormatPreviewHost), new PropertyMetadata(null, OnChanged));

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

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FormatPreviewHost)d).Refresh();

    private void Refresh()
    {
        if (FormatId != _shownFormatId)
        {
            _shownFormatId = FormatId;
            var formatUis = App.Services?.GetService<FormatUiRegistry>();
            _preview = formatUis?.Find(FormatId)?.CreateImportPreviewControl();
            Content = _preview?.View;
        }

        if (_preview != null && Order != null && Settings != null)
        {
            _preview.Show(Order, Settings);
            Visibility = Visibility.Visible;
        }
        else
        {
            _preview?.Clear();
            Visibility = Visibility.Collapsed;
        }
    }
}
