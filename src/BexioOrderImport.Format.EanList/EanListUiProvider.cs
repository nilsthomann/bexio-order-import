using BexioOrderImport.Abstractions.Wpf;

namespace BexioOrderImport.Format.EanList;

public sealed class EanListUiProvider : IFormatUiProvider
{
    public string FormatId => EanListFormatAdapter.Id;

    public IProfileConfigControl CreateProfileConfigControl() => new EanListProfileConfigControl();

    public IImportPreviewControl CreateImportPreviewControl() => new EanListPreviewControl();
}
