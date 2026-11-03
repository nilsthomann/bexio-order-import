using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;

namespace BexioOrderImport.Format.Sgd;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class SgdProfileConfigControl : UserControl, IProfileConfigControl
{
    private readonly SgdProfileConfigViewModel _viewModel = new();

    public SgdProfileConfigControl()
    {
        InitializeComponent();
    }

    public FrameworkElement View => this;

    public void Load(IFormatSettings settings)
    {
        // The inputs are plain properties, so bind them after loading
        DataContext = null;
        _viewModel.Load((SgdSettings)settings);
        DataContext = _viewModel;
    }

    public IReadOnlyList<string> Validate() => _viewModel.Validate();

    public IFormatSettings Save() => _viewModel.Save();
}
