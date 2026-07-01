using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BexioOrderImport.Wpf.ViewModels;

namespace BexioOrderImport.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
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
        // Delay recalculation slightly to let the grid commit the cell value change to the model
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.UpdateTotalsSummary();
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
