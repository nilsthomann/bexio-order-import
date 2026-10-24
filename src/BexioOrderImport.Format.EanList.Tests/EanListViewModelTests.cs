using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.EanList.Resources;
using FluentAssertions;

namespace BexioOrderImport.Format.EanList.Tests;

public class EanListViewModelTests
{
    [Test]
    public void ProfileConfig_LoadAndSave_WithoutChanges_ShouldKeepAllValues()
    {
        var settings = new EanListSettings { WorksheetIndex = 2, StartRow = 4, EanColumn = "C", QuantityColumn = "D", UseSalesUnitColumn = true, SalesUnitColumn = "G", CustomerNumber = "1001", DefaultOrderName = "Order {CustomerName}", PositionTextTemplate = "{EAN}" };
        var expected = new EanListSettings { WorksheetIndex = 2, StartRow = 4, EanColumn = "C", QuantityColumn = "D", UseSalesUnitColumn = true, SalesUnitColumn = "G", CustomerNumber = "1001", DefaultOrderName = "Order {CustomerName}", PositionTextTemplate = "{EAN}" };
        var viewModel = new EanListProfileConfigViewModel();

        viewModel.Load(settings);
        viewModel.Validate().Should().BeEmpty();

        viewModel.Save().Should().BeEquivalentTo(expected);
    }

    [Test]
    public void ProfileConfig_Save_ShouldTrimAndNormalizeColumns()
    {
        var viewModel = new EanListProfileConfigViewModel();
        viewModel.Load(new EanListSettings());
        viewModel.CustomerNumber = " 1001 ";
        viewModel.WorksheetIndex = " 3 ";
        viewModel.EanColumn = "b";
        viewModel.QuantityColumn = "3";
        viewModel.UseSalesUnitColumn = true;
        viewModel.SalesUnitColumn = "e";
        viewModel.DefaultOrderName = " Order ";

        viewModel.Validate().Should().BeEmpty();
        var saved = viewModel.Save();

        saved.CustomerNumber.Should().Be("1001");
        saved.WorksheetIndex.Should().Be(3);
        saved.EanColumn.Should().Be("B");
        saved.QuantityColumn.Should().Be("C");
        saved.UseSalesUnitColumn.Should().BeTrue();
        saved.SalesUnitColumn.Should().Be("E");
        saved.DefaultOrderName.Should().Be("Order");
    }

    [Test]
    public void ProfileConfig_Validate_ShouldReportInvalidNumbersAndColumns()
    {
        var viewModel = new EanListProfileConfigViewModel();
        viewModel.Load(new EanListSettings());
        viewModel.WorksheetIndex = "0";
        viewModel.StartRow = "x";
        viewModel.EanColumn = "1A";
        viewModel.QuantityColumn = "";
        viewModel.CustomerNumber = " ";

        var errors = viewModel.Validate();

        errors.Should().HaveCount(5);
        errors[0].Should().Contain(EanListTranslations.Settings_WorksheetLabel.TrimEnd(':'));
        errors[2].Should().Contain(EanListTranslations.Settings_EanColumn);
        errors[4].Should().Be(string.Format(EanListTranslations.Validation_Required, EanListTranslations.Settings_CustomerNumberLabel.TrimEnd(':')));
    }

    [Test]
    public void ProfileConfig_Validate_ShouldCheckTheSalesUnitColumnOnlyWhenItIsUsed()
    {
        var viewModel = new EanListProfileConfigViewModel();
        viewModel.Load(new EanListSettings { CustomerNumber = "1001" });
        viewModel.SalesUnitColumn = "#";

        viewModel.Validate().Should().BeEmpty();

        viewModel.UseSalesUnitColumn = true;
        viewModel.Validate().Should().ContainSingle().Which.Should().Contain(EanListTranslations.Settings_SalesUnitColumn);
    }

    [Test]
    public void ProfileConfig_Load_WhenSettingsAreNull_ShouldThrow()
    {
        Action act = () => new EanListProfileConfigViewModel().Load(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Preview_ShouldShowNumbersPositionsAndTotals()
    {
        var order = new Order
        {
            CustomerNumber = "1001",
            SkippedRows = 2,
            Positions = [new OrderPosition { Ean = "1", Quantity = 2, SalesUnit = 6 }, new OrderPosition { Ean = "2", Quantity = 3 }]
        };

        var preview = new EanListPreviewViewModel(order, new EanListSettings());

        preview.CustomerNumber.Should().Be("1001");
        preview.IsCustomerNumberMissing.Should().BeFalse();
        preview.Positions.Should().BeSameAs(order.Positions);
        preview.TotalQuantity.Should().Be(15, "2 × VE 6 + 3 × VE 1 pieces are imported");
        preview.SkippedRows.Should().Be(2);
    }

    [Test]
    public void Preview_WhenCustomerNumberIsMissing_ShouldWarn()
    {
        var preview = new EanListPreviewViewModel(new Order(), new EanListSettings());

        preview.CustomerNumber.Should().Be("-");
        preview.IsCustomerNumberMissing.Should().BeTrue();
    }

    [Test]
    public void Preview_WhenArgumentIsNull_ShouldThrow()
    {
        ((Action)(() => _ = new EanListPreviewViewModel(null!, new EanListSettings()))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new EanListPreviewViewModel(new Order(), null!))).Should().Throw<ArgumentNullException>();
    }
}
