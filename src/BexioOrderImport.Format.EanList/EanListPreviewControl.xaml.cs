using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.EanList;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class EanListPreviewControl : UserControl, IImportPreviewControl
{
    public EanListPreviewControl()
    {
        InitializeComponent();
    }

    public FrameworkElement View => this;

    public void Show(Order order, IFormatSettings settings)
    {
        DataContext = new EanListPreviewViewModel(order, (EanListSettings)settings);
    }

    public void Clear()
    {
        DataContext = null;
    }
}
