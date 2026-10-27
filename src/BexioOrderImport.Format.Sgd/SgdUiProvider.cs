using BexioOrderImport.Abstractions.Wpf;

namespace BexioOrderImport.Format.Sgd;

public sealed class SgdUiProvider : IFormatUiProvider
{
    public string FormatId => SgdFormatAdapter.Id;

    public IProfileConfigControl CreateProfileConfigControl() => new SgdProfileConfigControl();

    public IImportPreviewControl CreateImportPreviewControl() => new SgdPreviewControl();
}
