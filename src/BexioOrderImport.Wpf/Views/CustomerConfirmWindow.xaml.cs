using System.Windows;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.Views;

public partial class CustomerConfirmWindow : Window
{
    public CustomerConfirmWindow(Customer customer)
    {
        InitializeComponent();
        DataContext = customer;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
