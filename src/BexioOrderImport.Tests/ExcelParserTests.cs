using BexioOrderImport.Application.Options;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Infrastructure.Excel;
using BexioOrderImport.Wpf.Services;
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
    public void MapCategoryName_ShouldWorkForVariousInputs()
    {
        // Get the private MapCategoryName method using reflection
        var method = typeof(ClosedXmlExcelParser).GetMethod("MapCategoryName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        method.Should().NotBeNull();

        var categories = new[] { "Hats/Necks", "Mittens/Acc", "Socks/UWear", "Shoes 32-42", "Shoes 20-31", "Other Category" };

        // Test 1: Null or whitespace
        method!.Invoke(_parser, ["", categories]).Should().BeNull();
        method.Invoke(_parser, [null!, categories]).Should().BeNull();

        // Test 2: Exact Match
        method.Invoke(_parser, ["Other Category", categories]).Should().Be("Other Category");

        // Test 3: Acc/Mitten robust mapping
        method.Invoke(_parser, ["Mittens", categories]).Should().Be("Mittens/Acc");
        method.Invoke(_parser, ["Acc", categories]).Should().Be("Mittens/Acc");

        // Test 4: Socks/UWear robust mapping
        method.Invoke(_parser, ["Socks", categories]).Should().Be("Socks/UWear");
        method.Invoke(_parser, ["UWear", categories]).Should().Be("Socks/UWear");

        // Test 5: Shoes robust mapping
        method.Invoke(_parser, ["Shoes 32", categories]).Should().Be("Shoes 32-42");
        method.Invoke(_parser, ["Shoes 20", categories]).Should().Be("Shoes 20-31");

        // Test 6: StartsWith / Contains robust mapping fallback
        method.Invoke(_parser, ["Other", categories]).Should().Be("Other Category");

        // Test 7: No match
        method.Invoke(_parser, ["Unregistered Category", categories]).Should().BeNull();
    }

    [Fact]
    public void ExtractZipAndCity_ShouldHandleEdgeCases()
    {
        var extractZip = typeof(ClosedXmlExcelParser).GetMethod("ExtractZip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var extractCity = typeof(ClosedXmlExcelParser).GetMethod("ExtractCity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        extractZip!.Invoke(_parser, [""]).Should().Be("");
        extractCity!.Invoke(_parser, [""]).Should().Be("");

        extractZip.Invoke(_parser, ["8000"]).Should().Be("8000");
        extractCity.Invoke(_parser, ["8000"]).Should().Be("");
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
}
