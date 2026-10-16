using BexioOrderImport.Abstractions.Wpf;

namespace BexioOrderImport.Format.Peakmile;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class PeakmileUiProvider : IFormatUiProvider
{
    public string FormatId => PeakmileFormatAdapter.Id;

    public IProfileConfigControl CreateProfileConfigControl() => new PeakmileProfileConfigControl();

    public IImportPreviewControl CreateImportPreviewControl() => new PeakmilePreviewControl();
}
