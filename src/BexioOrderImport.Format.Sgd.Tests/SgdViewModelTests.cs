using System.Globalization;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Sgd.Resources;
using FluentAssertions;
using static BexioOrderImport.Format.Sgd.Tests.Utils.SgdTestSettings;

namespace BexioOrderImport.Format.Sgd.Tests;

[NotInParallel]
public class SgdViewModelTests
{
    private static SgdProfileConfigViewModel Loaded(SgdSettings? settings = null)
    {
        var viewModel = new SgdProfileConfigViewModel();
        viewModel.Load(settings ?? WithSeasons());
        return viewModel;
    }

    [Test]
    public void Load_ThenSave_ShouldKeepTheSettings()
    {
        var settings = WithSeasons(new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 3, ArticleNumberColumn = "F", UseExternalOrderNumberColumn = true, ExternalOrderNumberColumn = "G", SeasonColumn = "H",
            DiscountPercent = 10m, PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition, SizeRowTemplate = ""
        });
        var expected = WithSeasons(new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 3, ArticleNumberColumn = "F", UseExternalOrderNumberColumn = true, ExternalOrderNumberColumn = "G", SeasonColumn = "H",
            DiscountPercent = 10m, PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition
        });

        var viewModel = Loaded(settings);

        viewModel.SeasonMappings.Should().BeEquivalentTo(settings.SeasonMappings, o => o.WithStrictOrdering());
        viewModel.SeasonMappings[0].Should().NotBeSameAs(settings.SeasonMappings[0], "the editor works on copies");
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
        viewModel.SeasonColumn = "h";
        viewModel.SeasonMappings[0].ExternalSeason = " 2026 Fall/Winter ";
        viewModel.SeasonMappings[0].SeasonCode = " HW26 ";
        viewModel.DiscountPercent = " ";
        viewModel.IsSinglePosition = true;

        var settings = viewModel.Save();

        settings.WorksheetIndex.Should().Be(3);
        settings.QuantityColumn.Should().Be("AA");
        settings.ColorColumn.Should().Be("C");
        settings.SeasonColumn.Should().Be("H");
        settings.SeasonMappings[0].Should().BeEquivalentTo(new SgdSeasonMapping { ExternalSeason = FallWinter, SeasonCode = "HW26" });
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
    public void Validate_WhenTheTitleUsesTheExternalOrderNumber_ShouldNeedItsColumn()
    {
        var viewModel = Loaded(WithSeasons(new SgdSettings { DefaultOrderName = "Order {ExternalOrderNumber}" }));

        viewModel.Validate().Should().Equal(SgdTranslations.Validation_ExternalOrderNumberColumnRequired);

        viewModel.UseExternalOrderNumberColumn = true;
        viewModel.ExternalOrderNumberColumn = "#";
        viewModel.Validate().Should().Equal(string.Format(SgdTranslations.Validation_InvalidColumn, SgdTranslations.Settings_ExternalOrderNumberColumn));

        viewModel.ExternalOrderNumberColumn = "g";
        viewModel.Validate().Should().BeEmpty();
        viewModel.Save().ExternalOrderNumberColumn.Should().Be("G");
    }

    [Test]
    public void AddSeasonMappingCommand_ShouldNeedBothInputsAndAddTheTrimmedMapping()
    {
        var viewModel = Loaded(new SgdSettings());
        var command = viewModel.AddSeasonMappingCommand;

        command.CanExecute(null).Should().BeFalse();
        viewModel.NewExternalSeason = " 2026 Fall/Winter ";
        command.CanExecute(null).Should().BeFalse("the season code is missing");
        viewModel.NewSeasonCode = " ";
        command.CanExecute(null).Should().BeFalse();
        viewModel.NewSeasonCode = " HW26";
        command.CanExecute(null).Should().BeTrue();

        command.Execute(null);

        viewModel.SeasonMappings.Should().ContainSingle().Which.Should().BeEquivalentTo(new SgdSeasonMapping { ExternalSeason = FallWinter, SeasonCode = "HW26" });
        viewModel.NewExternalSeason.Should().BeEmpty();
        viewModel.NewSeasonCode.Should().BeEmpty();
        viewModel.Validate().Should().BeEmpty();
    }

    [Test]
    public void RemoveSeasonMappingCommand_ShouldRemoveTheMapping()
    {
        var viewModel = Loaded();
        var first = viewModel.SeasonMappings[0];

        viewModel.RemoveSeasonMappingCommand.Execute(first);

        viewModel.SeasonMappings.Should().ContainSingle().Which.ExternalSeason.Should().Be(SpringSummer);
    }

    [Test]
    public void Load_ShouldClearTheNewMappingInputs()
    {
        var viewModel = Loaded();
        viewModel.NewExternalSeason = "x";
        viewModel.NewSeasonCode = null!;

        viewModel.Load(new SgdSettings { SeasonMappings = null! });

        viewModel.NewExternalSeason.Should().BeEmpty();
        viewModel.NewSeasonCode.Should().BeEmpty();
        viewModel.SeasonMappings.Should().BeEmpty();
    }

    [Test]
    public void Validate_ShouldReportInvalidSeasonInputs()
    {
        var viewModel = Loaded(new SgdSettings { DefaultOrderName = "Order {SeasonCode}" });
        viewModel.SeasonColumn = "1A";

        viewModel.Validate().Should().Equal(
            string.Format(SgdTranslations.Validation_InvalidColumn, SgdTranslations.Settings_SeasonColumn),
            SgdTranslations.Validation_SeasonMappingRequired,
            SgdTranslations.Validation_SeasonCodePlaceholder);
    }

    [Test]
    public void Validate_ShouldReportIncompleteAndDuplicateMappings()
    {
        var viewModel = Loaded();
        viewModel.SeasonMappings.Add(new SgdSeasonMapping { ExternalSeason = " 2026 FALL/WINTER", SeasonCode = "X" });
        viewModel.SeasonMappings.Add(new SgdSeasonMapping { ExternalSeason = " ", SeasonCode = "Y" });
        viewModel.SeasonMappings.Add(new SgdSeasonMapping { ExternalSeason = "Z", SeasonCode = "" });

        viewModel.Validate().Should().Equal(
            SgdTranslations.Validation_SeasonMappingIncomplete,
            string.Format(SgdTranslations.Validation_DuplicateExternalSeason, FallWinter));
    }

    [Test]
    [Arguments("2028 Spring/Summer", "")]
    [Arguments("", "FS28")]
    public void Validate_WhenANewMappingIsNotAdded_ShouldReportIt(string externalSeason, string seasonCode)
    {
        var viewModel = Loaded();
        viewModel.NewExternalSeason = externalSeason;
        viewModel.NewSeasonCode = seasonCode;

        viewModel.Validate().Should().Equal(SgdTranslations.Validation_SeasonMappingNotAdded);
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
            Positions =
            [
                new OrderPosition { Quantity = 2, SeasonCode = "HW26" },
                new OrderPosition { Quantity = 3, SeasonCode = "FS27" },
                new OrderPosition { Quantity = 0, SeasonCode = "hw26" }
            ]
        };

        var viewModel = new SgdPreviewViewModel(order, new SgdSettings { DiscountPercent = 10m });

        viewModel.SeasonCodes.Should().Be("HW26, FS27");
        viewModel.Discount.Should().Be("10 %");
        viewModel.Positions.Should().BeSameAs(order.Positions);
        viewModel.TotalQuantity.Should().Be(5);
        viewModel.SkippedRows.Should().Be(2);
    }

    [Test]
    public void PreviewViewModel_WithoutPositionsAndDiscount_ShouldShowDashes()
    {
        var viewModel = new SgdPreviewViewModel(new Order(), new SgdSettings { DiscountPercent = 0m });

        viewModel.SeasonCodes.Should().Be("-");
        viewModel.Discount.Should().Be("-");
        viewModel.ShowExternalOrderNumber.Should().BeFalse();
        viewModel.ExternalOrderNumber.Should().Be("-");
        viewModel.HasSeveralExternalOrderNumbers.Should().BeFalse();
        viewModel.SeveralExternalOrderNumbersWarning.Should().BeEmpty();
    }

    [Test]
    public void PreviewViewModel_WithSeveralExternalOrderNumbers_ShouldShowTheFirstAndWarn()
    {
        var order = new Order { ExternalOrderNumbers = ["PO-1", "PO-2"] };

        var viewModel = new SgdPreviewViewModel(order, new SgdSettings { UseExternalOrderNumberColumn = true });

        viewModel.ShowExternalOrderNumber.Should().BeTrue();
        viewModel.ExternalOrderNumber.Should().Be("PO-1");
        viewModel.HasSeveralExternalOrderNumbers.Should().BeTrue();
        viewModel.SeveralExternalOrderNumbersWarning.Should().Be(string.Format(SgdTranslations.Preview_MultipleExternalOrderNumbers, "PO-1, PO-2"));
    }

    [Test]
    public void PreviewViewModel_WhenArgumentsAreNull_ShouldThrow()
    {
        ((Action)(() => _ = new SgdPreviewViewModel(null!, new SgdSettings()))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new SgdPreviewViewModel(new Order(), null!))).Should().Throw<ArgumentNullException>();
    }
}
