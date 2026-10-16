using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// Shows the customer and order number and the positions of an EAN list with one shared <see cref="EanListPreviewViewModel"/>.
/// </summary>
public sealed class EanListPreviewControl : IImportPreviewControl
{
    public FrameworkElement HeaderView { get; } = new EanListOrderHeaderControl();

    public FrameworkElement PositionsView { get; } = new EanListPositionsControl();

    public void Show(Order order, IFormatSettings settings)
    {
        var viewModel = new EanListPreviewViewModel(order, (EanListSettings)settings);
        HeaderView.DataContext = viewModel;
        PositionsView.DataContext = viewModel;
    }

    public void Clear()
    {
        HeaderView.DataContext = null;
        PositionsView.DataContext = null;
    }
}
