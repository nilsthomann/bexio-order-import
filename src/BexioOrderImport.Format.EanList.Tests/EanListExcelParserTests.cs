using BexioOrderImport.Format.EanList.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Format.EanList.Tests;

public class EanListExcelParserTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "EanListOrder.xlsx");

    private static readonly EanListExcelParser DefaultParser = new(new EanListSettings());

    [Test]
    public void ParseOrderForm_WithFixture_ShouldReadThePositions()
    {
        var order = DefaultParser.ParseOrderForm(FixturePath);

        order.Positions.Select(p => p.Ean).Should().Equal("7612345678900", "0012345678905", "4006381333931", "0826710896036");
        order.Positions.Select(p => p.Quantity).Should().Equal(3, 2, 1, 5);
        order.Positions.Select(p => p.SourceRow).Should().Equal(2, 3, 8, 9);
        order.SkippedRows.Should().Be(3, "rows with quantity 0, -1 and empty are ignored");
    }

    [Test]
    public void ParseOrderForm_ShouldNotChangeTheFixture()
    {
        byte[] before = File.ReadAllBytes(FixturePath);

        DefaultParser.ParseOrderForm(FixturePath);

        File.ReadAllBytes(FixturePath).Should().Equal(before);
    }

    [Test]
    public void ParseOrderForm_ShouldNotReadAnOrderNumber()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 1; sheet.Cell("C2").Value = "1001"; sheet.Cell("D2").Value = "AU-1";
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.OrderNumber.Should().BeNull("every EAN list import creates a new order");
    }

    [Test]
    public void ParseOrderForm_ShouldTakeTheCustomerNumberFromTheProfile()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 1; sheet.Cell("C2").Value = "9999";
        });

        var order = new EanListExcelParser(new EanListSettings { CustomerNumber = "1001" }).ParseOrderForm(file.Path);

        order.CustomerNumber.Should().Be("1001", "customer numbers in the file are ignored");
    }

    [Test]
    public void ParseOrderForm_ShouldNormalizeEansToEan13()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = 4006381333931d; sheet.Cell("B2").Value = 1;
            sheet.Cell("A3").Value = "40 0638 1333 931"; sheet.Cell("B3").Value = 1;
            sheet.Cell("A4").Value = 826710896036d; sheet.Cell("B4").Value = 1;
            sheet.Cell("A5").Value = 12345678905d; sheet.Cell("B5").Value = 1;
            sheet.Cell("A6").Value = "826710896036"; sheet.Cell("B6").Value = 1;
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.Positions.Select(p => p.Ean).Should().Equal("4006381333931", "4006381333931", "0826710896036", "0012345678905", "0826710896036");
    }

    [Test]
    [Arguments("4006381333932")]
    [Arguments("826710896037")]
    [Arguments("40063813339310")]
    [Arguments("40063A")]
    public void ParseOrderForm_WhenEanIsNoValidEan13_ShouldThrowWithEanAndRow(string ean)
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 1;
            sheet.Cell("A3").Value = ean; sheet.Cell("B3").Value = 1;
        });

        Action act = () => DefaultParser.ParseOrderForm(file.Path);

        act.Should().Throw<FormatException>().WithMessage($"*\"{ean}\"*3*EAN-13*");
    }

    [Test]
    public void ParseOrderForm_WhenNumberCellHasAWrongCheckDigit_ShouldThrow()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = 826710896037d; sheet.Cell("B2").Value = 1;
        });

        Action act = () => DefaultParser.ParseOrderForm(file.Path);

        act.Should().Throw<FormatException>().WithMessage("*826710896037*2*");
    }

    [Test]
    public void ParseOrderForm_ShouldAcceptQuantitiesAsText()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = " 4 ";
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.Positions.Should().ContainSingle().Which.Quantity.Should().Be(4);
    }

    [Test]
    [Arguments("1.5")]
    [Arguments("abc")]
    public void ParseOrderForm_WhenQuantityIsNoWholeNumber_ShouldThrowWithRow(string quantity)
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931";
            if (quantity == "1.5") sheet.Cell("B2").Value = 1.5; else sheet.Cell("B2").Value = quantity;
        });

        Action act = () => DefaultParser.ParseOrderForm(file.Path);

        act.Should().Throw<FormatException>().WithMessage("*2*");
    }

    [Test]
    public void ParseOrderForm_WhenEanIsMissing_ShouldThrowWithRow()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("B2").Value = 3;
        });

        Action act = () => DefaultParser.ParseOrderForm(file.Path);

        act.Should().Throw<FormatException>().WithMessage("*2*");
    }

    [Test]
    public void ParseOrderForm_ShouldUseTheConfiguredSheetRowsAndColumns()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ean_list_custom_{Guid.NewGuid():N}.xlsx");
        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        {
            workbook.AddWorksheet("Info");
            var sheet = workbook.AddWorksheet("Daten");
            sheet.Cell("B5").Value = 7; sheet.Cell("D5").Value = "4006381333931";
            workbook.SaveAs(path);
        }
        var parser = new EanListExcelParser(new EanListSettings
        {
            WorksheetIndex = 2, StartRow = 5, EanColumn = "D", QuantityColumn = "B"
        });

        try
        {
            var order = parser.ParseOrderForm(path);

            order.Positions.Should().ContainSingle().Which.Quantity.Should().Be(7);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ParseOrderForm_WhenFileIsMissing_ShouldThrow()
    {
        Action act = () => DefaultParser.ParseOrderForm("missing.xlsx");

        act.Should().Throw<FileNotFoundException>();
    }

    [Test]
    public void Constructor_WhenSettingsAreNull_ShouldThrow()
    {
        Action act = () => _ = new EanListExcelParser(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
