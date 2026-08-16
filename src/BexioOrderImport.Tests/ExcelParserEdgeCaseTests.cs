using BexioOrderImport.Application.Options;
using BexioOrderImport.Infrastructure.Excel;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BexioOrderImport.Tests;

public class ExcelParserEdgeCaseTests
{
    [Fact]
    public void ParseOrderForm_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        var parser = new ClosedXmlExcelParser(Options.Create(new ExcelMappingOptions()));

        Action act = () => parser.ParseOrderForm("non_existent_file.xlsx");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ParseOrderForm_WithInvalidWorksheetIndex_ThrowsInvalidOperationException()
    {
        var options = new ExcelMappingOptions
        {
            WorksheetIndex = 99
        };

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                wb.AddWorksheet("Sheet1");
                wb.SaveAs(tempPath);
            }

            var parser = new ClosedXmlExcelParser(Options.Create(options));

            Action act = () => parser.ParseOrderForm(tempPath);

            act.Should().Throw<ArgumentException>();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ParseOrderForm_WithMatrixHavingEmptyAndNonNumericCells_IgnoresInvalidQty()
    {
        var options = new ExcelMappingOptions();
        options.Header.CompanyNameCell = "B1";
        options.Header.StreetCell = "B2";
        options.Header.ZipCityCell = "B3";
        options.Header.BuyerEmailCell = "B4";
        options.Header.BuyerNameCell = "B5";
        options.Header.OrderIdCell = "B6";
        options.Header.PaymentTermsCell = "B7";
        options.Header.DiscountCell = "B8";

        options.SizeMatrix.StartRow = 10;
        options.SizeMatrix.EndRow = 10;
        options.SizeMatrix.CategoryColumn = "D";
        options.SizeMatrix.StartSizeColumn = "E";
        options.SizeMatrix.EndSizeColumn = "F";

        options.Data.StartRow = 11;
        options.Data.ArticleNumberColumn = "A";
        options.Data.ArticleNameColumn = "B";
        options.Data.ColorColumn = "C";
        options.Data.CategoryColumn = "D";
        options.Data.StartQtyColumn = "E";
        options.Data.EndQtyColumn = "F";
        options.Data.UnitPriceColumn = "G";

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.AddWorksheet("Sheet1");
                ws.Cell("B1").Value = "ACME Corp";
                ws.Cell("B4").Value = "test@acme.com";

                // Matrix header at row 10
                ws.Cell(10, 4).Value = "CAT1";
                ws.Cell(10, 5).Value = "S";
                ws.Cell(10, 6).Value = "M";

                // Row 11: Valid data, but Qty in Col 5 is non-numeric "INVALID", Col 6 is numeric 5
                ws.Cell(11, 1).Value = "ART001";
                ws.Cell(11, 2).Value = "Shirt";
                ws.Cell(11, 3).Value = "Blue";
                ws.Cell(11, 4).Value = "CAT1";
                ws.Cell(11, 5).Value = "INVALID";
                ws.Cell(11, 6).Value = 5;
                ws.Cell(11, 7).Value = 29.90;

                wb.SaveAs(tempPath);
            }

            var parser = new ClosedXmlExcelParser(Options.Create(options));
            var order = parser.ParseOrderForm(tempPath);

            order.Should().NotBeNull();
            order.Customer.CompanyName.Should().Be("ACME Corp");
            order.Positions.Should().HaveCount(1);
            order.Positions[0].Size.Should().Be("M");
            order.Positions[0].Quantity.Should().Be(5);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
