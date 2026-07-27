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
}
