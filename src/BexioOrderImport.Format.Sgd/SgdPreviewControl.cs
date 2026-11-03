using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Shows the profile values and the positions of an SGD list with one shared <see cref="SgdPreviewViewModel"/>.
/// </summary>
public sealed class SgdPreviewControl : IImportPreviewControl
{
    public FrameworkElement HeaderView { get; } = new SgdOrderHeaderControl();

    public FrameworkElement PositionsView { get; } = new SgdPositionsControl();

    public void Show(Order order, IFormatSettings settings)
    {
        var viewModel = new SgdPreviewViewModel(order, (SgdSettings)settings);
        HeaderView.DataContext = viewModel;
        PositionsView.DataContext = viewModel;
    }

    public void Clear()
    {
        HeaderView.DataContext = null;
        PositionsView.DataContext = null;
    }
}
