using BexioOrderImport.Format.Sgd.Resources;
using BexioOrderImport.Format.Sgd.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Format.Sgd.Tests;

public class SgdExcelParserTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SgdOrder.xlsx");

    [Test]
    public void ParseOrderForm_ShouldReadOnePositionPerRow()
    {
        var order = new SgdExcelParser(new SgdSettings()).ParseOrderForm(FixturePath);

        order.Positions.Select(p => (p.SourceRow, p.ArticleNumber, p.Color, p.Size, p.Quantity)).Should().Equal(
            (2, "E231001", "1001 Black", "S", 2),
            (3, "E231001", "1001 Black", "M", 3),
            (4, "E231001", "2048 Navy", "M", 1),
            (5, "230455", "3001 Red", "L", 4),
            (8, "230455", "3001 Red", "L", 1),
            (9, "E240777", "Black", "38", 2));
        order.Positions[0].ArticleName.Should().Be("Endurance Run Jacket");
        order.SkippedRows.Should().Be(1, "row 7 has the quantity 0, the empty row 6 is not counted");
        order.CustomerNumber.Should().BeNull("the customer number is asked for on import");
    }

    [Test]
    public void ParseOrderForm_WhenGrouped_ShouldGroupTheSizesOfAnArticleAndColor()
    {
        var settings = new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition, SizeRowTemplate = "{Amount} x {Size}" };

        var order = new SgdExcelParser(settings).ParseOrderForm(FixturePath);

        order.Positions.Select(p => (p.SourceRow, p.ArticleNumber, p.Color, p.Size, p.Quantity)).Should().Equal(
            (2, "E231001", "1001 Black", "2 x S<br />3 x M", 5),
            (4, "E231001", "2048 Navy", "1 x M", 1),
            (5, "230455", "3001 Red", "5 x L", 5),
            (9, "E240777", "Black", "2 x 38", 2));
    }

    [Test]
    public void ParseOrderForm_WhenGroupedWithoutSizeRowTemplate_ShouldUseTheDefault()
    {
        var settings = new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition, SizeRowTemplate = " " };

        var order = new SgdExcelParser(settings).ParseOrderForm(FixturePath);

        order.Positions[0].Size.Should().Be("2x Size S<br />3x Size M");
    }

    [Test]
    public void ParseOrderForm_WhenGrouped_ShouldIgnoreTheCaseOfArticleNumberColorAndSize()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "e1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("D2").Value = "m"; sheet.Cell("E2").Value = 1;
            sheet.Cell("A3").Value = "E1"; sheet.Cell("C3").Value = "BLACK"; sheet.Cell("D3").Value = "M"; sheet.Cell("E3").Value = 2;
        });

        var order = new SgdExcelParser(new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition }).ParseOrderForm(workbook.Path);

        order.Positions.Should().ContainSingle().Which.Size.Should().Be("3x Size m");
    }

    [Test]
    public void ParseOrderForm_ShouldUseTheConfiguredWorksheetStartRowAndColumns()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            var other = sheet.Workbook.AddWorksheet("SGD");
            other.Cell("B5").Value = 7;
            other.Cell("C5").Value = "A-1";
            other.Cell("D5").Value = "Red";
            other.Cell("E5").Value = 38.5;
            other.Cell("F5").Value = "Shirt";
        });
        var settings = new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 5,
            QuantityColumn = "B", ArticleNumberColumn = "C", ColorColumn = "D", SizeColumn = "E", ArticleNameColumn = "F"
        };

        var position = new SgdExcelParser(settings).ParseOrderForm(workbook.Path).Positions.Should().ContainSingle().Subject;

        position.Should().BeEquivalentTo(new { ArticleNumber = "A-1", ArticleName = "Shirt", Color = "Red", Size = "38.5", Quantity = 7, SourceRow = 5 });
    }

    [Test]
    public void ParseOrderForm_ShouldWriteLongArticleNumbersInFull()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = 1234567890123d;
            sheet.Cell("C2").Value = "Black";
            sheet.Cell("E2").Value = "3";
        });

        var position = new SgdExcelParser(new SgdSettings()).ParseOrderForm(workbook.Path).Positions.Single();

        position.ArticleNumber.Should().Be("1234567890123");
        position.Quantity.Should().Be(3, "a quantity stored as text is read too");
        position.Size.Should().BeEmpty("one-size articles have no size");
    }

    [Test]
    [Arguments("abc")]
    [Arguments("1.5")]
    public void ParseOrderForm_WhenQuantityIsNotAWholeNumber_ShouldThrowWithTheRow(string quantity)
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1";
            sheet.Cell("C2").Value = "Black";
            if (quantity == "1.5") sheet.Cell("E2").Value = 1.5; else sheet.Cell("E2").Value = quantity;
        });

        Action act = () => new SgdExcelParser(new SgdSettings()).ParseOrderForm(workbook.Path);

        // The value is written with the culture of the cell, so only the row is compared exactly
        act.Should().Throw<FormatException>().WithMessage(string.Format(SgdTranslations.Error_InvalidQuantity, "*", 2));
    }

    [Test]
    public void ParseOrderForm_ShouldSkipRowsWithoutQuantity()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black";
            sheet.Cell("A3").Value = "E2"; sheet.Cell("C3").Value = "Black"; sheet.Cell("E3").Value = "  ";
            sheet.Cell("A4").Value = "E3"; sheet.Cell("C4").Value = "Black"; sheet.Cell("E4").Value = -1;
        });

        var order = new SgdExcelParser(new SgdSettings()).ParseOrderForm(workbook.Path);

        order.Positions.Should().BeEmpty();
        order.SkippedRows.Should().Be(3);
    }

    [Test]
    public void ParseOrderForm_WhenArticleNumberIsMissing_ShouldThrowWithTheRow()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("C2").Value = "Black";
            sheet.Cell("E2").Value = 1;
        });

        Action act = () => new SgdExcelParser(new SgdSettings()).ParseOrderForm(workbook.Path);

        act.Should().Throw<FormatException>().WithMessage(string.Format(SgdTranslations.Error_MissingArticleNumber, 2));
    }

    [Test]
    public void ParseOrderForm_WhenColorIsMissing_ShouldThrowWithTheRow()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A3").Value = "E1";
            sheet.Cell("E3").Value = 1;
        });

        Action act = () => new SgdExcelParser(new SgdSettings()).ParseOrderForm(workbook.Path);

        act.Should().Throw<FormatException>().WithMessage(string.Format(SgdTranslations.Error_MissingColor, 3));
    }

    [Test]
    public void ParseOrderForm_WhenFileIsMissing_ShouldThrow()
    {
        Action act = () => new SgdExcelParser(new SgdSettings()).ParseOrderForm(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Test]
    public void Constructor_WhenSettingsAreNull_ShouldThrow()
    {
        Action act = () => _ = new SgdExcelParser(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
