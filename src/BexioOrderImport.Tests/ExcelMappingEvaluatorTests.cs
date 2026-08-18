using BexioOrderImport.Application.Options;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class ExcelMappingEvaluatorTests
{
    [Test]
    [Arguments("A1", true)]
    [Arguments("B12", true)]
    [Arguments("Z999", true)]
    [Arguments("AA100", true)]
    [Arguments("a1", true)]
    [Arguments("z50", true)]
    [Arguments("", false)]
    [Arguments("   ", false)]
    [Arguments(null, false)]
    [Arguments("123", false)]
    [Arguments("ABC", false)]
    [Arguments("A0", false)]
    [Arguments("A-1", false)]
    public void IsValidCellAddress_ShouldValidateCorrectly(string? address, bool expectedResult)
    {
        // Act
        bool result = ExcelMappingEvaluator.IsValidCellAddress(address);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Test]
    [Property("Category", "Unit")]
    public void CopyOptions_ShouldCopyAllValues()
    {
        // Arrange
        var source = new ExcelMappingOptions
        {
            WorksheetIndex = 2,
            DefaultOrderName = "Test Order",
            SeasonCode = "SS26",
            PositionTextTemplate = "{ArtNo}",
            DiscountPositionTextTemplate = "Disc",
            SizeRowTemplate = "{Amount}x {Size}",
            PositionGroupingMode = PositionGroupingMode.GroupedSizePosition,
            Header = new HeaderMapping
            {
                CompanyNameCell = "A1",
                StreetCell = "A2",
                ZipCityCell = "A3",
                BuyerEmailCell = "A4",
                BuyerNameCell = "A5",
                EnableOrderId = true,
                OrderIdCell = "A6",
                EnableCustomerId = true,
                CustomerIdCell = "A9",
                PaymentTermsCell = "A7",
                DiscountCell = "A8"
            },
            SizeMatrix = new SizeMatrixMapping
            {
                StartRow = 5,
                EndRow = 12,
                CategoryColumn = "B",
                StartSizeColumn = "C",
                EndSizeColumn = "J"
            },
            Data = new DataMapping
            {
                StartRow = 15,
                ArticleNumberColumn = "A",
                ArticleNameColumn = "B",
                ColorColumn = "C",
                CategoryColumn = "D",
                StartQtyColumn = "E",
                EndQtyColumn = "L",
                UnitPriceColumn = "M",
                EnableRowDiscount = true,
                RowDiscountColumn = "N"
            }
        };

        var target = new ExcelMappingOptions();

        // Act
        ExcelMappingEvaluator.CopyOptions(source, target);

        // Assert
        target.WorksheetIndex.Should().Be(2);
        target.DefaultOrderName.Should().Be("Test Order");
        target.SeasonCode.Should().Be("SS26");
        target.PositionGroupingMode.Should().Be(PositionGroupingMode.GroupedSizePosition);
        target.SizeRowTemplate.Should().Be("{Amount}x {Size}");
        target.Header.CompanyNameCell.Should().Be("A1");
        target.Header.BuyerEmailCell.Should().Be("A4");
        target.Header.EnableOrderId.Should().BeTrue();
        target.Header.EnableCustomerId.Should().BeTrue();
        target.Header.CustomerIdCell.Should().Be("A9");
        target.Data.EnableRowDiscount.Should().BeTrue();
        target.Data.RowDiscountColumn.Should().Be("N");
    }

    [Test]
    [Property("Category", "Unit")]
    public void CopyOptions_WhenSourceOrTargetNull_ShouldThrowArgumentNullException()
    {
        // Act
        var actNullSource = () => ExcelMappingEvaluator.CopyOptions(null!, new ExcelMappingOptions());
        var actNullTarget = () => ExcelMappingEvaluator.CopyOptions(new ExcelMappingOptions(), null!);

        // Assert
        actNullSource.Should().Throw<ArgumentNullException>();
        actNullTarget.Should().Throw<ArgumentNullException>();
    }

    [Test]
    [Property("Category", "Unit")]
    public void CloneOptions_ShouldCreateDeepCopy()
    {
        // Arrange
        var source = new ExcelMappingOptions
        {
            DefaultOrderName = "Clone Test",
            SeasonCode = "FW25"
        };

        // Act
        var clone = ExcelMappingEvaluator.CloneOptions(source);

        // Assert
        clone.Should().NotBeNull();
        clone.Should().NotBeSameAs(source);
        clone.DefaultOrderName.Should().Be("Clone Test");
        clone.SeasonCode.Should().Be("FW25");
    }
}
