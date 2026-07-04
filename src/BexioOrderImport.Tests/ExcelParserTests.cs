using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Options;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Infrastructure.Excel;
using BexioOrderImport.Domain.Models;

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
                DeliveryDateCell = "T7",
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

    private string FindExcelFile(string filename)
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
        order.Customer.Email.Should().Be("orders@musterfashion.ch");
        order.Customer.BuyerName.Should().Be("Hans Muster");

        // 2. Delivery & payment terms assertions
        order.DeliveryDate.Should().Be(new DateTime(2026, 5, 28));
        order.PaymentTerms.Should().Be("10 Tage 4% Skonto, 30 Tage netto");

        // 3. Totals assertions
        order.TotalQuantity.Should().Be(103);
        // Verify gross total
        order.TotalAmount.Should().Be(2228.00m);
        // Using approximate comparison for decimals due to rounding differences (global Excel discount vs position-level rounding)
        order.TotalNetAmount.Should().BeApproximately(2049.76m, 0.01m);
        order.Positions.Should().HaveCount(101);

        // 4. Detail assertion for a specific order item (e.g. Baby Sandals row 400)
        // Col 1: 760403, Col 2: Baby Sandals (now Anonymized Sandals), Col 3: 4202 Zephyr (now Anonymized 4202 Zephyr), Col 4: Shoes 20-31
        // Qty in Col 5 (Size 20) is 1. Qty in Col 6 (Size 21) is 1.
        var sandalPos = order.Positions.FirstOrDefault(p => 
            p.ArticleNumber == "760403" && 
            p.Color == "Anonymized 4202 Zephyr" && 
            p.Size == "20");

        sandalPos.Should().NotBeNull();
        sandalPos!.ArticleName.Should().Be("Anonymized Sandals");
        sandalPos.SizeCategory.Should().Be("Shoes 20-31");
        sandalPos.Quantity.Should().Be(1);
        sandalPos.UnitPrice.Should().Be(9.10m);
        sandalPos.TotalPrice.Should().Be(9.10m);
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
    public void InMemoryExcelParser_ShouldReturnProvidedOrder()
    {
        // Arrange
        var expectedOrder = new Order();
        expectedOrder.Positions.Add(new OrderPosition { Quantity = 42 });
        var parser = new BexioOrderImport.Wpf.Services.InMemoryExcelParser(expectedOrder);

        // Act
        var order = parser.ParseOrderForm("anypath.xlsx");

        // Assert
        order.Should().BeSameAs(expectedOrder);
    }

    [Fact]
    public void MapCategoryName_ShouldWorkForVariousInputs()
    {
        // Get the private MapCategoryName method using reflection
        var method = typeof(ClosedXmlExcelParser).GetMethod("MapCategoryName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull();

        var categories = new[] { "Hats/Necks", "Mittens/Acc", "Socks/UWear", "Shoes 32-42", "Shoes 20-31", "Other Category" };

        // Test 1: Null or whitespace
        method!.Invoke(_parser, new object[] { "", categories }).Should().BeNull();
        method.Invoke(_parser, new object[] { null!, categories }).Should().BeNull();

        // Test 2: Exact Match
        method.Invoke(_parser, new object[] { "Other Category", categories }).Should().Be("Other Category");

        // Test 3: Acc/Mitten robust mapping
        method.Invoke(_parser, new object[] { "Mittens", categories }).Should().Be("Mittens/Acc");
        method.Invoke(_parser, new object[] { "Acc", categories }).Should().Be("Mittens/Acc");

        // Test 4: Socks/UWear robust mapping
        method.Invoke(_parser, new object[] { "Socks", categories }).Should().Be("Socks/UWear");
        method.Invoke(_parser, new object[] { "UWear", categories }).Should().Be("Socks/UWear");

        // Test 5: Shoes robust mapping
        method.Invoke(_parser, new object[] { "Shoes 32", categories }).Should().Be("Shoes 32-42");
        method.Invoke(_parser, new object[] { "Shoes 20", categories }).Should().Be("Shoes 20-31");

        // Test 6: StartsWith / Contains robust mapping fallback
        method.Invoke(_parser, new object[] { "Other", categories }).Should().Be("Other Category");
        
        // Test 7: No match
        method.Invoke(_parser, new object[] { "Unregistered Category", categories }).Should().BeNull();
    }

    [Fact]
    public void ExtractZipAndCity_ShouldHandleEdgeCases()
    {
        var extractZip = typeof(ClosedXmlExcelParser).GetMethod("ExtractZip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var extractCity = typeof(ClosedXmlExcelParser).GetMethod("ExtractCity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        extractZip!.Invoke(_parser, new object[] { "" }).Should().Be("");
        extractCity!.Invoke(_parser, new object[] { "" }).Should().Be("");

        extractZip.Invoke(_parser, new object[] { "8000" }).Should().Be("8000");
        extractCity.Invoke(_parser, new object[] { "8000" }).Should().Be("");
    }

    private string CreateTemporaryExcelFile(string deliveryDateStr, string discountStr)
    {
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Order Form");
            
            // Header mappings
            sheet.Cell("B4").Value = "Test Company";
            sheet.Cell("B5").Value = "Test Street";
            sheet.Cell("B6").Value = "8000 Zurich";
            sheet.Cell("E5").Value = "test@test.com";
            sheet.Cell("E4").Value = "Buyer Name";
            sheet.Cell("T7").Value = deliveryDateStr;
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
    }

    [Fact]
    public void ParseOrderForm_WithDecimalDiscount_ShouldScaleToPercentage()
    {
        // Arrange
        string filePath = CreateTemporaryExcelFile("2026-05-28", "0.05");

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
    public void ParseOrderForm_WithInvalidDeliveryDate_ShouldReturnNullDeliveryDate()
    {
        // Arrange
        string filePath = CreateTemporaryExcelFile("invalid-date-value", "5%");

        try
        {
            // Act
            var order = _parser.ParseOrderForm(filePath);

            // Assert
            order.DeliveryDate.Should().BeNull();
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }
}
