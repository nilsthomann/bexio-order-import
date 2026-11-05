using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BexioOrderImport.Wpf.Models;
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

    private Point? _dragStart;

    private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        // The grip only drags; selecting the row would leave it highlighted grey after the drop
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragStart = null;
            return;
        }
        if (_dragStart is not Point start || DataContext is not MainViewModel vm || sender is not FrameworkElement { DataContext: MappingProfile profile }) return;

        Vector moved = e.GetPosition(this) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragStart = null;
        vm.BeginProfileDrag(profile);
        // The grid is the drag source, as the row of the handle is replaced while the profile moves
        var effect = DragDrop.DoDragDrop(ProfilesGrid, profile, DragDropEffects.Move);
        vm.EndProfileDrag(effect == DragDropEffects.Move);
    }

    private void ProfilesGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        // Only profiles dragged in this list are accepted, eg. no files
        if (DataContext is not MainViewModel { DraggedProfile: not null } vm)
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Move;
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(ProfilesGrid, source) is DataGridRow row)
        {
            // The placeholder row takes the place of the row under the mouse, so the mouse stays on it
            vm.MoveDraggedProfile(row.GetIndex());
        }
    }

    private void ProfilesGrid_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DataContext is MainViewModel { DraggedProfile: not null } ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Helpers.WindowHelper.OpenUrl(e.Uri);
        e.Handled = true;
    }
}
