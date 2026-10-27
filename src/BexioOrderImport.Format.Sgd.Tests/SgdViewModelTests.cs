using System.Globalization;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Sgd.Resources;
using FluentAssertions;

namespace BexioOrderImport.Format.Sgd.Tests;

[NotInParallel]
public class SgdViewModelTests
{
    private static SgdProfileConfigViewModel Loaded(SgdSettings? settings = null)
    {
        var viewModel = new SgdProfileConfigViewModel();
        viewModel.Load(settings ?? new SgdSettings());
        return viewModel;
    }

    [Test]
    public void Load_ThenSave_ShouldKeepTheSettings()
    {
        var settings = new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 3, ArticleNumberColumn = "F", SeasonCode = "SS27", SeasonFallbackCodes = ["NOOS", "EX"],
            DiscountPercent = 10m, PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition, SizeRowTemplate = ""
        };
        var expected = new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 3, ArticleNumberColumn = "F", SeasonCode = "SS27", SeasonFallbackCodes = ["NOOS", "EX"],
            DiscountPercent = 10m, PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition
        };

        var viewModel = Loaded(settings);

        viewModel.SeasonFallbackCodes.Should().Be("NOOS, EX");
        viewModel.IsGroupedPositions.Should().BeTrue();
        viewModel.IsSinglePosition.Should().BeFalse();
        viewModel.Validate().Should().BeEmpty();
        viewModel.Save().Should().BeEquivalentTo(expected, "an empty size row template is loaded as the default");
    }

    [Test]
    public void Save_ShouldTrimAndNormalizeTheInputs()
    {
        var viewModel = Loaded();
        viewModel.WorksheetIndex = " 3 ";
        viewModel.QuantityColumn = "aa";
        viewModel.ColorColumn = "3";
        viewModel.SeasonCode = " FS28 ";
        viewModel.SeasonFallbackCodes = "NOOS; EX, ,";
        viewModel.DiscountPercent = " ";
        viewModel.IsSinglePosition = true;

        var settings = viewModel.Save();

        settings.WorksheetIndex.Should().Be(3);
        settings.QuantityColumn.Should().Be("AA");
        settings.ColorColumn.Should().Be("C");
        settings.SeasonCode.Should().Be("FS28");
        settings.SeasonFallbackCodes.Should().Equal("NOOS", "EX");
        settings.DiscountPercent.Should().Be(0m, "an empty discount is none");
        settings.PositionGroupingMode.Should().Be(SgdPositionGroupingMode.SinglePositionPerSize);
    }

    [Test]
    [Arguments("7.5", 7.5)]
    [Arguments("0", 0)]
    [Arguments("100", 100)]
    public void Save_ShouldReadTheDiscountWithADecimalPoint(string input, double expected)
    {
        var viewModel = Loaded();
        viewModel.DiscountPercent = input;

        viewModel.Validate().Should().BeEmpty();
        viewModel.Save().DiscountPercent.Should().Be((decimal)expected);
    }

    [Test]
    public void Save_InACultureWithADecimalComma_ShouldNotReadADecimalPointAsThousands()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var viewModel = Loaded();

            viewModel.DiscountPercent = "7.5";
            viewModel.Save().DiscountPercent.Should().Be(7.5m);
            viewModel.DiscountPercent = "7,5";
            viewModel.Save().DiscountPercent.Should().Be(7.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public void Validate_ShouldReportEveryInvalidInput()
    {
        var viewModel = Loaded();
        viewModel.WorksheetIndex = "0";
        viewModel.StartRow = "x";
        viewModel.ArticleNumberColumn = "A1";
        viewModel.ArticleNameColumn = "";
        viewModel.ColorColumn = "-";
        viewModel.SizeColumn = "?";
        viewModel.QuantityColumn = "0";
        viewModel.DiscountPercent = "101";
        viewModel.IsGroupedPositions = true;
        viewModel.GroupedPositionTextTemplate = "{BexioArticleName}";

        var errors = viewModel.Validate();

        errors.Should().HaveCount(9);
        errors.Should().Contain(SgdTranslations.Validation_InvalidDiscount);
        errors.Should().Contain(SgdTranslations.Validation_SizesRowsRequired);
        errors.Should().Contain(string.Format(SgdTranslations.Validation_InvalidColumn, SgdTranslations.Settings_SizeColumn));
    }

    [Test]
    [Arguments("-5")]
    [Arguments("abc")]
    public void Validate_WhenTheDiscountIsNotBetween0And100_ShouldReportIt(string discount)
    {
        var viewModel = Loaded();
        viewModel.DiscountPercent = discount;

        viewModel.Validate().Should().Equal(SgdTranslations.Validation_InvalidDiscount);
    }

    [Test]
    public void IsGroupedPositions_ShouldNotifyBothModes()
    {
        var viewModel = Loaded();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IsGroupedPositions = true;
        viewModel.IsGroupedPositions = true;

        changed.Should().Equal(nameof(SgdProfileConfigViewModel.IsGroupedPositions), nameof(SgdProfileConfigViewModel.IsSinglePosition));
    }

    [Test]
    public void Load_WhenSettingsAreNull_ShouldThrow()
    {
        Action act = () => new SgdProfileConfigViewModel().Load(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void PreviewViewModel_ShouldShowTheProfileValuesAndTotals()
    {
        var order = new Order
        {
            SkippedRows = 2,
            Positions = [new OrderPosition { Quantity = 2 }, new OrderPosition { Quantity = 3 }]
        };

        var viewModel = new SgdPreviewViewModel(order, new SgdSettings { SeasonCode = "FS27", DiscountPercent = 10m });

        viewModel.SeasonCode.Should().Be("FS27");
        viewModel.Discount.Should().Be("10 %");
        viewModel.Positions.Should().BeSameAs(order.Positions);
        viewModel.TotalQuantity.Should().Be(5);
        viewModel.SkippedRows.Should().Be(2);
    }

    [Test]
    public void PreviewViewModel_WithoutSeasonCodeAndDiscount_ShouldShowDashes()
    {
        var viewModel = new SgdPreviewViewModel(new Order(), new SgdSettings { SeasonCode = " ", DiscountPercent = 0m });

        viewModel.SeasonCode.Should().Be("-");
        viewModel.Discount.Should().Be("-");
    }

    [Test]
    public void PreviewViewModel_WhenArgumentsAreNull_ShouldThrow()
    {
        ((Action)(() => _ = new SgdPreviewViewModel(null!, new SgdSettings()))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new SgdPreviewViewModel(new Order(), null!))).Should().Throw<ArgumentNullException>();
    }
}
