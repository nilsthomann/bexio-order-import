using BexioOrderImport.Application.Options;
using BexioOrderImport.Application.Services;
using FluentAssertions;
using Xunit;

namespace BexioOrderImport.Tests;

public class ExcelMappingEvaluatorTests
{
    [Theory]
    [InlineData("A1", true)]
    [InlineData("B12", true)]
    [InlineData("Z999", true)]
    [InlineData("AA100", true)]
    [InlineData("a1", true)]
    [InlineData("z50", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("123", false)]
    [InlineData("ABC", false)]
    [InlineData("A0", false)]
    [InlineData("A-1", false)]
    public void IsValidCellAddress_ShouldValidateCorrectly(string? address, bool expectedResult)
    {
        // Act
        bool result = ExcelMappingEvaluator.IsValidCellAddress(address);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    [Trait("Category", "Unit")]
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
                CategoryColumn = 2,
                StartSizeColumn = 3,
                EndSizeColumn = 10
            },
            Data = new DataMapping
            {
                StartRow = 15,
                ArticleNumberColumn = 1,
                ArticleNameColumn = 2,
                ColorColumn = 3,
                CategoryColumn = 4,
                StartQtyColumn = 5,
                EndQtyColumn = 12,
                UnitPriceColumn = 13,
                EnableRowDiscount = true,
                RowDiscountColumn = 14
            }
        };

        var target = new ExcelMappingOptions();

        // Act
        ExcelMappingEvaluator.CopyOptions(source, target);

        // Assert
        target.WorksheetIndex.Should().Be(2);
        target.DefaultOrderName.Should().Be("Test Order");
        target.SeasonCode.Should().Be("SS26");
        target.Header.CompanyNameCell.Should().Be("A1");
        target.Header.BuyerEmailCell.Should().Be("A4");
        target.Header.EnableOrderId.Should().BeTrue();
        target.Header.EnableCustomerId.Should().BeTrue();
        target.Header.CustomerIdCell.Should().Be("A9");
        target.Data.EnableRowDiscount.Should().BeTrue();
        target.Data.RowDiscountColumn.Should().Be(14);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CopyOptions_WhenSourceOrTargetNull_ShouldThrowArgumentNullException()
    {
        // Act
        var actNullSource = () => ExcelMappingEvaluator.CopyOptions(null!, new ExcelMappingOptions());
        var actNullTarget = () => ExcelMappingEvaluator.CopyOptions(new ExcelMappingOptions(), null!);

        // Assert
        actNullSource.Should().Throw<ArgumentNullException>();
        actNullTarget.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
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
