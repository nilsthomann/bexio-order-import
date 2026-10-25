using System.Windows;
using BexioOrderImport.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BexioOrderImport.Wpf.Views;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Helpers.WindowHelper.OpenUrl(e.Uri);
        e.Handled = true;
    }
}
