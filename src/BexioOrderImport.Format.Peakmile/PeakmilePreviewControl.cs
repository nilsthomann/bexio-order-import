using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.Peakmile;

/// <summary>
/// Shows the order header and the positions of a Peakmile order with one shared <see cref="PeakmilePreviewViewModel"/>.
/// </summary>
public sealed class PeakmilePreviewControl : IImportPreviewControl
{
    public FrameworkElement HeaderView { get; } = new PeakmileOrderHeaderControl();

    public FrameworkElement PositionsView { get; } = new PeakmilePositionsControl();

    public void Show(Order order, IFormatSettings settings)
    {
        var viewModel = new PeakmilePreviewViewModel(order, (PeakmileSettings)settings);
        HeaderView.DataContext = viewModel;
        PositionsView.DataContext = viewModel;
    }

    public void Clear()
    {
        HeaderView.DataContext = null;
        PositionsView.DataContext = null;
    }
}
