using ClosedXML.Excel;

namespace BexioOrderImport.Format.Sgd.Tests.Utils;

/// <summary>
/// A temp workbook for edge cases with the header row of an SGD list; deleted on dispose.
/// </summary>
public sealed class TempWorkbook : IDisposable
{
    public TempWorkbook(Action<IXLWorksheet> fill)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sgd_{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Bestellung");
        sheet.Cell("A1").Value = "Artikelnummer";
        sheet.Cell("B1").Value = "Artikelname";
        sheet.Cell("C1").Value = "Farbe";
        sheet.Cell("D1").Value = "Grösse";
        sheet.Cell("E1").Value = "Menge";
        fill(sheet);
        workbook.SaveAs(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }
}
