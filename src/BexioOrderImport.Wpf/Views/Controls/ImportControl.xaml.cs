using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BexioOrderImport.Wpf.ViewModels;

namespace BexioOrderImport.Wpf.Views.Controls;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class ImportControl : UserControl
{
    public ImportControl()
    {
        InitializeComponent();
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && DataContext is MainViewModel vm)
            {
                _ = vm.LoadExcelFileAsync(files[0]);
            }
        }
    }

    private void DropZone_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _ = vm.LoadExcelFileAsync();
        }
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.ScrollToEnd();
        }
    }

    private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.UpdateTotalsSummary();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ProfileItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Models.MappingProfile profile && DataContext is MainViewModel vm)
        {
            vm.SetActiveProfileCommand.Execute(profile);
            ProfileDropdownButton.IsChecked = false;
        }
    }
}
