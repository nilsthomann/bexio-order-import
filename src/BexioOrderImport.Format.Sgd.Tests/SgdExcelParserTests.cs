using BexioOrderImport.Format.Sgd.Resources;
using BexioOrderImport.Format.Sgd.Tests.Utils;
using FluentAssertions;
using static BexioOrderImport.Format.Sgd.Tests.Utils.SgdTestSettings;

namespace BexioOrderImport.Format.Sgd.Tests;

public class SgdExcelParserTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SgdOrder.xlsx");

    [Test]
    public void ParseOrderForm_ShouldReadOnePositionPerRow()
    {
        var order = new SgdExcelParser(WithSeasons()).ParseOrderForm(FixturePath);

        order.Positions.Select(p => (p.SourceRow, p.ArticleNumber, p.Color, p.Size, p.Quantity, p.SeasonCode)).Should().Equal(
            (2, "E231001", "1001 Black", "S", 2, "HW26"),
            (3, "E231001", "1001 Black", "M", 3, "HW26"),
            (4, "E231001", "2048 Navy", "M", 1, "HW26"),
            (5, "230455", "3001 Red", "L", 4, "FS27"),
            (8, "230455", "3001 Red", "L", 1, "FS27"),
            (9, "E240777", "Black", "38", 2, "HW26"));
        order.Positions[0].ArticleName.Should().Be("Endurance Run Jacket");
        order.SkippedRows.Should().Be(1, "row 7 has the quantity 0, the empty row 6 is not counted");
        order.CustomerNumber.Should().BeNull("the customer number is asked for on import");
    }

    [Test]
    public void ParseOrderForm_WhenGrouped_ShouldGroupTheSizesOfAnArticleAndColor()
    {
        var settings = WithSeasons(new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition, SizeRowTemplate = "{Amount} x {Size}" });

        var order = new SgdExcelParser(settings).ParseOrderForm(FixturePath);

        order.Positions.Select(p => (p.SourceRow, p.ArticleNumber, p.Color, p.Size, p.Quantity, p.SeasonCode)).Should().Equal(
            (2, "E231001", "1001 Black", "2 x S<br />3 x M", 5, "HW26"),
            (4, "E231001", "2048 Navy", "1 x M", 1, "HW26"),
            (5, "230455", "3001 Red", "5 x L", 5, "FS27"),
            (9, "E240777", "Black", "2 x 38", 2, "HW26"));
    }

    [Test]
    public void ParseOrderForm_WhenGrouped_ShouldKeepTheSameArticleOfAnotherSeasonApart()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("D2").Value = "M"; sheet.Cell("E2").Value = 1;
            sheet.Cell("A3").Value = "E1"; sheet.Cell("C3").Value = "Black"; sheet.Cell("D3").Value = "M"; sheet.Cell("E3").Value = 2; sheet.Cell("G3").Value = SpringSummer;
        });

        var order = new SgdExcelParser(WithSeasons(new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition })).ParseOrderForm(workbook.Path);

        order.Positions.Select(p => (p.SeasonCode, p.Quantity)).Should().Equal(("HW26", 1), ("FS27", 2));
    }

    [Test]
    public void ParseOrderForm_ShouldMapTheSeasonIgnoringCaseAndSurroundingWhitespace()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1; sheet.Cell("G2").Value = "  2027 SPRING/summer ";
        });
        var settings = WithSeasons();
        settings.SeasonMappings[1].ExternalSeason = " 2027 Spring/Summer ";
        settings.SeasonMappings[1].SeasonCode = " FS27 ";

        var order = new SgdExcelParser(settings).ParseOrderForm(workbook.Path);

        order.Positions.Single().SeasonCode.Should().Be("FS27");
    }

    [Test]
    public void ParseOrderForm_ShouldReadTheConfiguredSeasonColumn()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1; sheet.Cell("H2").Value = SpringSummer;
        }, fillSeason: false);

        var order = new SgdExcelParser(WithSeasons(new SgdSettings { SeasonColumn = "H" })).ParseOrderForm(workbook.Path);

        order.Positions.Single().SeasonCode.Should().Be("FS27");
    }

    [Test]
    public void ParseOrderForm_WhenSeasonsAreNotMapped_ShouldListThemAllWithTheirRows()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1; sheet.Cell("G2").Value = "2028 Spring/Summer";
            sheet.Cell("A3").Value = "E1"; sheet.Cell("C3").Value = "Black"; sheet.Cell("E3").Value = 1;
            sheet.Cell("A4").Value = "E2"; sheet.Cell("C4").Value = "Black"; sheet.Cell("E4").Value = 1; sheet.Cell("G4").Value = "2028 spring/summer";
            sheet.Cell("A5").Value = "E3"; sheet.Cell("C5").Value = "Black"; sheet.Cell("E5").Value = 1; sheet.Cell("G5").Value = "NOOS";
        });

        Action act = () => new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

        act.Should().Throw<FormatException>().Which.Message.Should().Be(string.Join(Environment.NewLine,
            SgdTranslations.Error_UnknownSeasons,
            string.Format(SgdTranslations.Error_UnknownSeasonRows, "2028 Spring/Summer", "2, 4"),
            string.Format(SgdTranslations.Error_UnknownSeasonRows, "NOOS", "5")));
    }

    [Test]
    public void ParseOrderForm_ShouldNotCheckTheSeasonOfSkippedRows()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1;
            sheet.Cell("A3").Value = "E2"; sheet.Cell("C3").Value = "Black"; sheet.Cell("E3").Value = 0; sheet.Cell("G3").Value = "Unknown";
        });

        var order = new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

        order.Positions.Should().ContainSingle();
        order.SkippedRows.Should().Be(1);
    }

    [Test]
    public void ParseOrderForm_WhenTheSeasonIsMissing_ShouldThrowWithTheRow()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1;
        }, fillSeason: false);

        Action act = () => new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

        act.Should().Throw<FormatException>().WithMessage(string.Format(SgdTranslations.Error_MissingSeason, 2));
    }

    [Test]
    public void ParseOrderForm_WithoutSeasonMappings_ShouldReportTheSeasons()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1;
        });

        Action act = () => new SgdExcelParser(new SgdSettings { SeasonMappings = null! }).ParseOrderForm(workbook.Path);

        act.Should().Throw<FormatException>().WithMessage($"{SgdTranslations.Error_UnknownSeasons}*{FallWinter}*");
    }

    [Test]
    public void ParseOrderForm_WhenGroupedWithoutSizeRowTemplate_ShouldUseTheDefault()
    {
        var settings = WithSeasons(new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition, SizeRowTemplate = " " });

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

        var order = new SgdExcelParser(WithSeasons(new SgdSettings { PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition })).ParseOrderForm(workbook.Path);

        order.Positions.Should().ContainSingle().Which.Size.Should().Be("3x Size m");
    }

    [Test]
    public void ParseOrderForm_WithExternalOrderNumberColumn_ShouldCollectTheDifferentNumbersInRowOrder()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1;
            sheet.Cell("A3").Value = "E1"; sheet.Cell("C3").Value = "Black"; sheet.Cell("E3").Value = 1; sheet.Cell("F3").Value = 4711;
            sheet.Cell("A4").Value = "E1"; sheet.Cell("C4").Value = "Black"; sheet.Cell("E4").Value = 1; sheet.Cell("F4").Value = " 4711 ";
            sheet.Cell("A5").Value = "E1"; sheet.Cell("C5").Value = "Black"; sheet.Cell("E5").Value = 0; sheet.Cell("F5").Value = "PO-2";
        });

        var order = new SgdExcelParser(WithSeasons(new SgdSettings { UseExternalOrderNumberColumn = true })).ParseOrderForm(workbook.Path);

        order.ExternalOrderNumbers.Should().Equal("4711", "PO-2");
        order.ExternalOrderNumber.Should().Be("4711", "the first number that is set counts");
    }

    [Test]
    public void ParseOrderForm_WithoutExternalOrderNumberColumn_ShouldNotReadIt()
    {
        using var workbook = new TempWorkbook(sheet =>
        {
            sheet.Cell("A2").Value = "E1"; sheet.Cell("C2").Value = "Black"; sheet.Cell("E2").Value = 1; sheet.Cell("F2").Value = "PO-1";
        });

        var order = new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

        order.ExternalOrderNumber.Should().BeNull();
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
            other.Cell("A5").Value = FallWinter;
        });
        var settings = WithSeasons(new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 5,
            QuantityColumn = "B", ArticleNumberColumn = "C", ColorColumn = "D", SizeColumn = "E", ArticleNameColumn = "F", SeasonColumn = "A"
        });

        var position = new SgdExcelParser(settings).ParseOrderForm(workbook.Path).Positions.Should().ContainSingle().Subject;

        position.Should().BeEquivalentTo(new { ArticleNumber = "A-1", ArticleName = "Shirt", Color = "Red", Size = "38.5", Quantity = 7, SourceRow = 5, SeasonCode = "HW26" });
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

        var position = new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path).Positions.Single();

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

        Action act = () => new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

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

        var order = new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

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

        Action act = () => new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

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

        Action act = () => new SgdExcelParser(WithSeasons()).ParseOrderForm(workbook.Path);

        act.Should().Throw<FormatException>().WithMessage(string.Format(SgdTranslations.Error_MissingColor, 3));
    }

    [Test]
    public void ParseOrderForm_WhenFileIsMissing_ShouldThrow()
    {
        Action act = () => new SgdExcelParser(WithSeasons()).ParseOrderForm(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx"));

        act.Should().Throw<FileNotFoundException>();
    }

    [Test]
    public void Constructor_WhenSettingsAreNull_ShouldThrow()
    {
        Action act = () => _ = new SgdExcelParser(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
