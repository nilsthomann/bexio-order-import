using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.Peakmile;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class PeakmilePreviewControl : UserControl, IImportPreviewControl
{
    public PeakmilePreviewControl()
    {
        InitializeComponent();
    }

    public FrameworkElement View => this;

    public void Show(Order order, IFormatSettings settings)
    {
        DataContext = new PeakmilePreviewViewModel(order, (PeakmileSettings)settings);
    }

    public void Clear()
    {
        DataContext = null;
    }
}
