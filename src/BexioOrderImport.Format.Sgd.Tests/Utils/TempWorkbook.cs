using ClosedXML.Excel;

namespace BexioOrderImport.Format.Sgd.Tests.Utils;

/// <summary>
/// A temp workbook for edge cases with the header row of an SGD list; deleted on dispose.
/// Rows with an article number and no season get <see cref="SgdTestSettings.FallWinter"/> unless fillSeason is false.
/// </summary>
public sealed class TempWorkbook : IDisposable
{
    public TempWorkbook(Action<IXLWorksheet> fill, bool fillSeason = true)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sgd_{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Bestellung");
        sheet.Cell("A1").Value = "Artikelnummer";
        sheet.Cell("B1").Value = "Artikelname";
        sheet.Cell("C1").Value = "Farbe";
        sheet.Cell("D1").Value = "Grösse";
        sheet.Cell("E1").Value = "Menge";
        sheet.Cell("G1").Value = "Saison";
        fill(sheet);
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; fillSeason && row <= lastRow; row++)
        {
            if (!sheet.Cell(row, "A").IsEmpty() && sheet.Cell(row, "G").IsEmpty())
            {
                sheet.Cell(row, "G").Value = SgdTestSettings.FallWinter;
            }
        }
        workbook.SaveAs(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (File.Exists(Path)) File.Delete(Path);
    }
}
