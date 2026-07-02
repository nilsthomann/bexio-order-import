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
                PaymentTermsCell = "A9"
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
}
