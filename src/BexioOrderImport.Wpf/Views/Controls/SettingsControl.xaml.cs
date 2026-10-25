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
        Helpers.WindowHelper.OpenUrl(e.Uri);
        e.Handled = true;
    }
}
