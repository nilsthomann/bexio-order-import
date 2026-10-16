using ClosedXML.Excel;

namespace BexioOrderImport.Format.EanList.Tests.Utils;

/// <summary>
/// A temp workbook for edge cases; deleted on dispose.
/// </summary>
public sealed class TempWorkbook : IDisposable
{
    public TempWorkbook(Action<IXLWorksheet> fill)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ean_list_{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("EAN-Liste");
        sheet.Cell("A1").Value = "EAN";
        sheet.Cell("B1").Value = "Menge";
        fill(sheet);
        workbook.SaveAs(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }
}
