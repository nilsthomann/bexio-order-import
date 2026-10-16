using BexioOrderImport.Format.EanList.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Format.EanList.Tests;

public class EanListExcelParserTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "EanListOrder.xlsx");

    private static readonly EanListExcelParser DefaultParser = new(new EanListSettings());

    [Test]
    public void ParseOrderForm_WithFixture_ShouldReadPositionsAndFirstRowNumbers()
    {
        var order = DefaultParser.ParseOrderForm(FixturePath);

        order.CustomerNumber.Should().Be("1001", "only the first data row counts, row 9 has another customer");
        order.OrderNumber.Should().BeNull("the order number of row 2 is empty, row 9 is ignored");
        order.Positions.Select(p => p.Ean).Should().Equal("7612345678904", "0012345678905", "4006381333931", "7612345678904");
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
    public void ParseOrderForm_ShouldReadTheOrderNumberOfTheFirstRow()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 1; sheet.Cell("C2").Value = "1001"; sheet.Cell("D2").Value = " AU-1 ";
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.OrderNumber.Should().Be("AU-1");
    }

    [Test]
    public void ParseOrderForm_WhenFirstRowHasNoCustomerNumber_ShouldIgnoreLaterRows()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 1;
            sheet.Cell("A3").Value = "4006381333931"; sheet.Cell("B3").Value = 1; sheet.Cell("C3").Value = "1001";
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.CustomerNumber.Should().BeNull();
    }

    [Test]
    public void ParseOrderForm_WhenFirstRowIsSkipped_ShouldStillReadItsCustomerNumber()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 0; sheet.Cell("C2").Value = 1001;
            sheet.Cell("A3").Value = "4006381333931"; sheet.Cell("B3").Value = 2;
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.CustomerNumber.Should().Be("1001");
        order.Positions.Should().ContainSingle().Which.SourceRow.Should().Be(3);
        order.SkippedRows.Should().Be(1);
    }

    [Test]
    public void ParseOrderForm_ShouldNormalizeEans()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = 12345678901231d; sheet.Cell("B2").Value = 1;
            sheet.Cell("A3").Value = "40 0638 1333 931"; sheet.Cell("B3").Value = 1;
            sheet.Cell("A4").Value = "00012345"; sheet.Cell("B4").Value = 1;
        });

        var order = DefaultParser.ParseOrderForm(file.Path);

        order.Positions.Select(p => p.Ean).Should().Equal("12345678901231", "4006381333931", "00012345");
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
    public void ParseOrderForm_WhenEanHasLetters_ShouldThrowWithRow()
    {
        using var file = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "4006381333931"; sheet.Cell("B2").Value = 1;
            sheet.Cell("A3").Value = "40063A"; sheet.Cell("B3").Value = 1;
        });

        Action act = () => DefaultParser.ParseOrderForm(file.Path);

        act.Should().Throw<FormatException>().WithMessage("*40063A*3*");
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
            sheet.Cell("B5").Value = 7; sheet.Cell("D5").Value = "4006381333931"; sheet.Cell("E5").Value = "1001"; sheet.Cell("F5").Value = "AU-2";
            workbook.SaveAs(path);
        }
        var parser = new EanListExcelParser(new EanListSettings
        {
            WorksheetIndex = 2, StartRow = 5, EanColumn = "D", QuantityColumn = "B", CustomerNumberColumn = "E", OrderNumberColumn = "F"
        });

        try
        {
            var order = parser.ParseOrderForm(path);

            order.CustomerNumber.Should().Be("1001");
            order.OrderNumber.Should().Be("AU-2");
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
