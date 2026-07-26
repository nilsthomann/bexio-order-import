using BexioOrderImport.Application.Options;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Infrastructure.Excel;
using BexioOrderImport.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class ExcelParserTests
{
    private readonly ClosedXmlExcelParser _parser;

    public ExcelParserTests()
    {
        // Setup default mapping options (matching appsettings.json)
        var options = new ExcelMappingOptions
        {
            WorksheetIndex = 1,
            Header = new HeaderMapping
            {
                CompanyNameCell = "B4",
                StreetCell = "B5",
                ZipCityCell = "B6",
                BuyerEmailCell = "E5",
                BuyerNameCell = "E4",
                OrderIdCell = "E6",
                PaymentTermsCell = "A9",
                DiscountCell = "V12"
            },
            SizeMatrix = new SizeMatrixMapping
            {
                StartRow = 10,
                EndRow = 17,
                CategoryColumn = 4,
                StartSizeColumn = 5,
                EndSizeColumn = 18
            },
            Data = new DataMapping
            {
                StartRow = 18,
                ArticleNumberColumn = 1,
                ArticleNameColumn = 2,
                ColorColumn = 3,
                CategoryColumn = 4,
                StartQtyColumn = 5,
                EndQtyColumn = 18,
                UnitPriceColumn = 20
            }
        };

        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        _parser = new ClosedXmlExcelParser(optionsWrapper);
    }

    private static string FindExcelFile(string filename)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string path = Path.Combine(dir, filename);
            if (File.Exists(path)) return path;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"Excel file {filename} not found in any parent directories.");
    }

    [Fact]
    public void ParseOrderForm_WithValidFile_ShouldExtractOrderCorrectly()
    {
        // Arrange
        string filePath = FindExcelFile("AnonymizedOrder.xlsx");

        // Restore original AnonymizedOrder.xlsx matrix category names
        using (var wb = new ClosedXML.Excel.XLWorkbook(filePath))
        {
            var ws = wb.Worksheet(1);
            ws.Cell("D10").Value = "Mittens/Acc";
            ws.Cell("D11").Value = "Hats/Necks";
            ws.Cell("D12").Value = "Shoes";
            ws.Cell("D13").Value = "Socks/UWear";
            ws.Cell("D14").Value = "Shoes 20-31";
            ws.Cell("D15").Value = "Shoes 32-41";
            ws.Cell("D16").Value = "Mini";
            ws.Cell("E5").Value = "chris@peakmile.com";
            wb.Save();
        }

        // Act
        var order = _parser.ParseOrderForm(filePath);

        // Assert
        order.Should().NotBeNull();

        // 1. Customer metadata assertions
        order.Customer.CompanyName.Should().Be("Muster Fashion AG");
        order.Customer.Street.Should().Be("Musterstrasse 12");
        order.Customer.ZipCode.Should().Be("8000");
        order.Customer.City.Should().Be("Zürich");
        order.Customer.Email.Should().Be("chris@peakmile.com");
        order.Customer.BuyerName.Should().Be("Hans Muster");

        // 2. Delivery & payment terms assertions
        order.OrderId.Should().BeNull();
        order.PaymentTerms.Should().Be("10 Tage 4% Skonto, 30 Tage netto");

        // 3. Totals assertions
        order.TotalQuantity.Should().Be(4);
        order.TotalAmount.Should().Be(72.8m);
        order.TotalNetAmount.Should().BeApproximately(66.98m, 0.01m);
        order.Positions.Should().HaveCount(4);

        // 4. Detail assertion for a specific order item
        var itemPos = order.Positions.FirstOrDefault(p =>
            p.ArticleNumber == "1234" &&
            p.Color == "Anonymized 4202 Zephyr" &&
            p.Size == "74");

        itemPos.Should().NotBeNull();
        itemPos!.ArticleName.Should().Be("T-Shirt");
        itemPos.SizeCategory.Should().Be("Mini");
        itemPos.Quantity.Should().Be(1);
        itemPos.UnitPrice.Should().Be(18.2m);
        itemPos.TotalPrice.Should().Be(18.2m);
    }

    [Fact]
    public void ParseOrderForm_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        string filePath = "non_existent_file.xlsx";

        // Act
        Action act = () => _parser.ParseOrderForm(filePath);

        // Assert
        act.Should().Throw<FileNotFoundException>()
           .WithMessage("*Excel file not found*");
    }

    [Fact]
    public void ParseOrderForm_WithFileStreamOpenWithReadWriteShare_ShouldSucceed()
    {
        // Arrange
        string filePath = Path.Combine(Path.GetTempPath(), $"test_share_{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Bestellformular");
                ws.Cell("B4").Value = "Test Company";
                ws.Cell("B5").Value = "Test Street";
                ws.Cell("B6").Value = "8000 Zurich";
                ws.Cell("E5").Value = "test@example.com";
                ws.Cell("E4").Value = "Test Buyer";
                ws.Cell("E6").Value = "1001";
                ws.Cell("A9").Value = "30 Tage netto";
                wb.SaveAs(filePath);
            }

            // Open file with FileShare.ReadWrite to simulate Excel open in read share
            using var fileLockStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Act
            var order = _parser.ParseOrderForm(filePath);

            // Assert
            order.Should().NotBeNull();
            order.Customer.CompanyName.Should().Be("Test Company");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public void InMemoryExcelParser_ShouldReturnProvidedOrder()
    {
        // Arrange
        var expectedOrder = new Order();
        expectedOrder.Positions.Add(new OrderPosition { Quantity = 42 });
        var parser = new InMemoryExcelParser(expectedOrder);

        // Act
        var order = parser.ParseOrderForm("anypath.xlsx");

        // Assert
        order.Should().BeSameAs(expectedOrder);
    }

    [Fact]
    public void ParseOrderForm_WhenCategoryNotInMatrix_ShouldThrowInvalidOperationException()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Order Form");

        sheet.Cell("B4").Value = "Test Company";
        sheet.Cell("B5").Value = "Test Street";
        sheet.Cell("B6").Value = "8000 Zurich";

        // Matrix has "ValidCategory"
        sheet.Cell("D10").Value = "ValidCategory";
        sheet.Cell("E10").Value = "S";

        // Data row has "UnknownCategory"
        sheet.Cell("A18").Value = "ART001";
        sheet.Cell("B18").Value = "Article 1";
        sheet.Cell("D18").Value = "UnknownCategory";
        sheet.Cell("E18").Value = 5;

        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        workbook.SaveAs(tempPath);

        try
        {
            Action act = () => _parser.ParseOrderForm(tempPath);
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Category 'UnknownCategory' on row 18 is not defined in the size matrix.*");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void ParseOrderForm_WhenSizeHeaderMissing_ShouldThrowInvalidOperationException()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Order Form");

        sheet.Cell("B4").Value = "Test Company";
        sheet.Cell("B5").Value = "Test Street";

        // Matrix has "CategoryA" with size only in col E (5), col F (6) is empty
        sheet.Cell("D10").Value = "CategoryA";
        sheet.Cell("E10").Value = "S";

        // Data row has quantity in col F (6), which has no size header defined
        sheet.Cell("A18").Value = "ART001";
        sheet.Cell("B18").Value = "Article 1";
        sheet.Cell("D18").Value = "CategoryA";
        sheet.Cell("F18").Value = 10;

        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        workbook.SaveAs(tempPath);

        try
        {
            Action act = () => _parser.ParseOrderForm(tempPath);
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Size header for column 6 in category 'CategoryA'*");
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void ExtractZipAndCity_ShouldHandleEdgeCases()
    {
        ClosedXmlExcelParser.ExtractZip("").Should().Be("");
        ClosedXmlExcelParser.ExtractCity("").Should().Be("");

        ClosedXmlExcelParser.ExtractZip("8000").Should().Be("8000");
        ClosedXmlExcelParser.ExtractCity("8000").Should().Be("");
    }

    private static string CreateTemporaryExcelFile(string orderIdStr, string discountStr)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Order Form");

        // Header mappings
        sheet.Cell("B4").Value = "Test Company";
        sheet.Cell("B5").Value = "Test Street";
        sheet.Cell("B6").Value = "8000 Zurich";
        sheet.Cell("E5").Value = "test@test.com";
        sheet.Cell("E4").Value = "Buyer Name";
        sheet.Cell("E6").Value = orderIdStr;
        sheet.Cell("A9").Value = "30 Days";
        sheet.Cell("V12").Value = discountStr;

        // Size matrix rows (10 - 17)
        sheet.Cell("D10").Value = "Other Category";
        sheet.Cell("E10").Value = "S";

        // Data rows (18+)
        sheet.Cell("A18").Value = "ART001";
        sheet.Cell("B18").Value = "Article 1";
        sheet.Cell("C18").Value = "Red";
        sheet.Cell("D18").Value = "Other Category";
        sheet.Cell("E18").Value = 5; // Quantity
        sheet.Cell("T18").Value = 10.0m; // Unit Price

        string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        workbook.SaveAs(tempPath);
        return tempPath;
    }

    [Fact]
    public void ParseOrderForm_WithDecimalDiscount_ShouldScaleToPercentage()
    {
        // Arrange
        string filePath = CreateTemporaryExcelFile("123", "0.05");

        try
        {
            // Act
            var order = _parser.ParseOrderForm(filePath);

            // Assert
            order.DiscountPercent.Should().Be(5m);
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }

    [Fact]
    public void ParseOrderForm_WithInvalidOrderId_ShouldReturnNullOrderId()
    {
        // Arrange
        string filePath = CreateTemporaryExcelFile("invalid-id-value", "5%");

        try
        {
            // Act
            var order = _parser.ParseOrderForm(filePath);

            // Assert
            order.OrderId.Should().BeNull();
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }

    [Fact]
    public void ParseOrderForm_WithValidOrderId_ShouldParseOrderIdCorrectly()
    {
        // Arrange
        string filePath = CreateTemporaryExcelFile("12345", "5%");

        try
        {
            // Act
            var order = _parser.ParseOrderForm(filePath);

            // Assert
            order.OrderId.Should().Be(12345);
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }

    [Fact]
    public void ParseOrderForm_WithRowDiscountEnabled_ShouldSetPositionDiscountPercent()
    {
        // Arrange
        var options = new ExcelMappingOptions();
        options.Data.EnableRowDiscount = true;
        options.Data.RowDiscountColumn = 21;
        var parser = new ClosedXmlExcelParser(Microsoft.Extensions.Options.Options.Create(options));

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Bestellformular");
        ws.Cell("B4").Value = "Firma AG";
        ws.Cell("B5").Value = "Musterstrasse 1";
        ws.Cell("B6").Value = "8000 Zurich";
        ws.Cell("E5").Value = "info@firma.ch";

        // Size matrix
        ws.Cell(10, 4).Value = "KIDS";
        ws.Cell(10, 5).Value = "92";

        // Data row
        ws.Cell(18, 1).Value = "ART-01";
        ws.Cell(18, 2).Value = "Jacke";
        ws.Cell(18, 3).Value = "Rot";
        ws.Cell(18, 4).Value = "KIDS";
        ws.Cell(18, 5).Value = 2; // Qty
        ws.Cell(18, 20).Value = 100m; // Unit Price
        ws.Cell(18, 21).Value = "15%"; // Row discount in Col 21

        string tempPath = Path.Combine(Path.GetTempPath(), $"row_disc_{Guid.NewGuid():N}.xlsx");
        wb.SaveAs(tempPath);

        try
        {
            // Act
            var order = parser.ParseOrderForm(tempPath);

            // Assert
            order.Positions.Should().HaveCount(1);
            order.Positions[0].GrossUnitPrice.Should().Be(100m);
            order.Positions[0].DiscountPercent.Should().Be(15m);
            order.Positions[0].NetUnitPrice.Should().Be(85m);
            order.Positions[0].TotalPrice.Should().Be(170m);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ParseOrderForm_WithRowDiscountDisabled_ShouldKeepPositionDiscountNull()
    {
        // Arrange
        var options = new ExcelMappingOptions();
        options.Data.EnableRowDiscount = false;
        options.Data.RowDiscountColumn = 21;
        var parser = new ClosedXmlExcelParser(Microsoft.Extensions.Options.Options.Create(options));

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Bestellformular");
        ws.Cell("B4").Value = "Firma AG";
        ws.Cell("B5").Value = "Musterstrasse 1";
        ws.Cell("B6").Value = "8000 Zurich";
        ws.Cell("E5").Value = "info@firma.ch";

        // Size matrix
        ws.Cell(10, 4).Value = "KIDS";
        ws.Cell(10, 5).Value = "92";

        // Data row
        ws.Cell(18, 1).Value = "ART-01";
        ws.Cell(18, 2).Value = "Jacke";
        ws.Cell(18, 3).Value = "Rot";
        ws.Cell(18, 4).Value = "KIDS";
        ws.Cell(18, 5).Value = 2; // Qty
        ws.Cell(18, 20).Value = 100m; // Unit Price
        ws.Cell(18, 21).Value = "15%"; // Col 21 ignored because row discount is disabled

        string tempPath = Path.Combine(Path.GetTempPath(), $"row_disc_dis_{Guid.NewGuid():N}.xlsx");
        wb.SaveAs(tempPath);

        try
        {
            // Act
            var order = parser.ParseOrderForm(tempPath);

            // Assert
            order.Positions.Should().HaveCount(1);
            order.Positions[0].GrossUnitPrice.Should().Be(100m);
            order.Positions[0].DiscountPercent.Should().BeNull();
            order.Positions[0].NetUnitPrice.Should().Be(100m);
            order.Positions[0].TotalPrice.Should().Be(200m);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseOrderForm_WithRowDiscountEnabled_ShouldCalculateNetUnitPriceAndTotalCorrectly()
    {
        // Arrange
        var options = new ExcelMappingOptions
        {
            Header = new HeaderMapping
            {
                CompanyNameCell = "A1",
                StreetCell = "A2",
                ZipCityCell = "A3",
                BuyerEmailCell = "A4",
                BuyerNameCell = "A5",
                OrderIdCell = "A6",
                PaymentTermsCell = "A7",
                DiscountCell = "A8"
            },
            SizeMatrix = new SizeMatrixMapping
            {
                StartRow = 10,
                EndRow = 10,
                CategoryColumn = 4,
                StartSizeColumn = 5,
                EndSizeColumn = 5
            },
            Data = new DataMapping
            {
                StartRow = 18,
                ArticleNumberColumn = 1,
                ArticleNameColumn = 2,
                ColorColumn = 3,
                CategoryColumn = 4,
                StartQtyColumn = 5,
                EndQtyColumn = 5,
                UnitPriceColumn = 6,
                EnableRowDiscount = true,
                RowDiscountColumn = 7
            }
        };

        var parser = new ClosedXmlExcelParser(Microsoft.Extensions.Options.Options.Create(options));

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");

        ws.Cell("A1").Value = "Test AG";
        ws.Cell(10, 4).Value = "KIDS";
        ws.Cell(10, 5).Value = "92";

        ws.Cell(18, 1).Value = "ART-02";
        ws.Cell(18, 2).Value = "Hose";
        ws.Cell(18, 3).Value = "Blau";
        ws.Cell(18, 4).Value = "KIDS";
        ws.Cell(18, 5).Value = 2; // Qty
        ws.Cell(18, 6).Value = 100m; // Unit Price
        ws.Cell(18, 7).Value = "20%"; // 20% discount

        string tempPath = Path.Combine(Path.GetTempPath(), $"row_disc_enabled_{Guid.NewGuid():N}.xlsx");
        wb.SaveAs(tempPath);

        try
        {
            // Act
            var order = parser.ParseOrderForm(tempPath);

            // Assert
            order.Should().NotBeNull();
            order.Positions.Should().HaveCount(1);
            order.Positions[0].GrossUnitPrice.Should().Be(100m);
            order.Positions[0].DiscountPercent.Should().Be(20m);
            order.Positions[0].NetUnitPrice.Should().Be(80m);
            order.Positions[0].TotalPrice.Should().Be(160m);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
