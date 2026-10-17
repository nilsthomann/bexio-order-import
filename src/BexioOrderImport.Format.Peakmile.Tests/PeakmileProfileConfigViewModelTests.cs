using BexioOrderImport.Format.Peakmile.Resources;
using FluentAssertions;

namespace BexioOrderImport.Format.Peakmile.Tests;

public class PeakmileProfileConfigViewModelTests
{
    private static PeakmileSettings CustomSettings() => new()
    {
        WorksheetIndex = 2,
        DefaultOrderName = "Auftrag {CustomerName}",
        SeasonCode = "HW26",
        SeasonFallbackCodes = ["NOOS", "EX"],
        SinglePositionTextTemplate = "{BexioArticleName} {Size}",
        GroupedPositionTextTemplate = "{BexioArticleName}<br />{SizesRows}",
        DiscountPositionTextTemplate = "Rabatt {DiscountInPercent}",
        SizeRowTemplate = "{Amount} x {Size}",
        PositionGroupingMode = PositionGroupingMode.GroupedSizePosition,
        Header = new HeaderMapping
        {
            CompanyNameCell = "C4",
            StreetCell = "C5",
            ZipCityCell = "C6",
            BuyerEmailCell = "F5",
            BuyerNameCell = "F4",
            EnableOrderNumber = true,
            OrderNumberCell = "F6",
            EnableCustomerNumber = true,
            CustomerNumberCell = "F3",
            PaymentTermsCell = "B9",
            DiscountCell = "W12"
        },
        SizeMatrix = new SizeMatrixMapping { StartRow = 11, EndRow = 18, CategoryColumn = "E", StartSizeColumn = "F", EndSizeColumn = "S" },
        Data = new DataMapping
        {
            StartRow = 19,
            ArticleNumberColumn = "B",
            ArticleNameColumn = "C",
            ColorColumn = "D",
            CategoryColumn = "E",
            StartQtyColumn = "F",
            EndQtyColumn = "S",
            UnitPriceColumn = "U",
            EnableRowDiscount = true,
            RowDiscountColumn = "V"
        }
    };

    [Test]
    public void LoadAndSave_WithoutChanges_ShouldKeepAllValues()
    {
        var expected = CustomSettings();
        var viewModel = new PeakmileProfileConfigViewModel();

        viewModel.Load(CustomSettings());
        viewModel.Validate().Should().BeEmpty();
        var saved = viewModel.Save();

        saved.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Load_ShouldShowTheSettingsAsText()
    {
        var viewModel = new PeakmileProfileConfigViewModel();

        viewModel.Load(CustomSettings());

        viewModel.WorksheetIndex.Should().Be("2");
        viewModel.SeasonFallbackCodes.Should().Be("NOOS, EX");
        viewModel.MatrixStartRow.Should().Be("11");
        viewModel.DataStartRow.Should().Be("19");
        viewModel.IsGroupedPositions.Should().BeTrue();
        viewModel.IsSinglePosition.Should().BeFalse();
    }

    [Test]
    public void Load_WhenSizeRowTemplateIsEmpty_ShouldUseTheDefault()
    {
        var viewModel = new PeakmileProfileConfigViewModel();

        viewModel.Load(new PeakmileSettings { SizeRowTemplate = " " });

        viewModel.SizeRowTemplate.Should().Be("{Amount}x Size {Size}");
    }

    [Test]
    public void Load_WhenSettingsAreNull_ShouldThrow()
    {
        var viewModel = new PeakmileProfileConfigViewModel();

        Action act = () => viewModel.Load(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Save_ShouldTrimInputsSplitFallbackCodesAndNormalizeColumns()
    {
        var viewModel = new PeakmileProfileConfigViewModel();
        viewModel.Load(new PeakmileSettings());
        viewModel.WorksheetIndex = " 3 ";
        viewModel.SeasonCode = " FS28 ";
        viewModel.SeasonFallbackCodes = "NOOS; EX, ,CORE";
        viewModel.CompanyNameCell = " B7 ";
        viewModel.ArticleNumberColumn = "c";
        viewModel.UnitPriceColumn = "#";

        var saved = viewModel.Save();

        saved.WorksheetIndex.Should().Be(3);
        saved.SeasonCode.Should().Be("FS28");
        saved.SeasonFallbackCodes.Should().Equal("NOOS", "EX", "CORE");
        saved.Header.CompanyNameCell.Should().Be("B7");
        saved.Data.ArticleNumberColumn.Should().Be("C");
        saved.Data.UnitPriceColumn.Should().Be("T", "an invalid column falls back to the default");
    }

    [Test]
    public void IsSinglePosition_ShouldBeTheInverseOfIsGroupedPositions()
    {
        var viewModel = new PeakmileProfileConfigViewModel();
        viewModel.Load(new PeakmileSettings());
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IsSinglePosition = false;

        viewModel.IsGroupedPositions.Should().BeTrue();
        changed.Should().Contain([nameof(PeakmileProfileConfigViewModel.IsGroupedPositions), nameof(PeakmileProfileConfigViewModel.IsSinglePosition)]);
        viewModel.Save().PositionGroupingMode.Should().Be(PositionGroupingMode.GroupedSizePosition);
    }

    [Test]
    public void Validate_WhenGroupedTemplateHasNoSizesRows_ShouldReportIt()
    {
        var viewModel = new PeakmileProfileConfigViewModel();
        viewModel.Load(new PeakmileSettings());
        viewModel.IsGroupedPositions = true;
        viewModel.GroupedPositionTextTemplate = "{BexioArticleName}";

        viewModel.Validate().Should().Equal(PeakmileTranslations.Dialog_SizesRowsPlaceholderRequiredForGrouped);
    }

    [Test]
    public void Validate_WhenSingleModeTemplateHasNoSizesRows_ShouldPass()
    {
        var viewModel = new PeakmileProfileConfigViewModel();
        viewModel.Load(new PeakmileSettings());
        viewModel.GroupedPositionTextTemplate = "{BexioArticleName}";

        viewModel.Validate().Should().BeEmpty();
    }

    [Test]
    [Arguments("abc")]
    [Arguments("0")]
    [Arguments("-1")]
    [Arguments("")]
    public void Validate_WhenNumbersAreInvalid_ShouldReportEachField(string value)
    {
        var viewModel = new PeakmileProfileConfigViewModel();
        viewModel.Load(new PeakmileSettings());
        viewModel.WorksheetIndex = value;
        viewModel.MatrixStartRow = value;
        viewModel.MatrixEndRow = value;
        viewModel.DataStartRow = value;

        var errors = viewModel.Validate();

        errors.Should().HaveCount(4);
        errors[0].Should().Contain(PeakmileTranslations.Settings_WorksheetLabel.TrimEnd(':', ' ')).And.NotContain(":\"");
        errors[3].Should().Contain(PeakmileTranslations.Settings_RowStart);
    }
}
