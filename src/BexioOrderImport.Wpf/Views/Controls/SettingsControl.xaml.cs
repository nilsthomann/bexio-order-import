using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Wpf.ViewModels;

namespace BexioOrderImport.Wpf.Views.Controls;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class SettingsControl : UserControl
{
    public SettingsControl()
    {
        InitializeComponent();
    }

    private void TokenTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsTokenFocused = true;
        }
    }

    private void TokenTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsTokenFocused = false;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            e.Handled = true;
        }
        catch
        {
            // Ignore navigation failure
        }
    }
}
