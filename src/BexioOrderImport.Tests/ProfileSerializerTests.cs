using System.Text.Json;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Wpf.Services;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class ProfileSerializerTests
{
    private readonly ProfileSerializer _serializer = new(new FormatRegistry([new PeakmileFormatAdapter()]));

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
        PositionGroupingMode = PositionGroupingMode.SinglePositionPerSize,
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
    public void FromDto_WhenProfileIsFromVersion16_ShouldLoadAsPeakmileWithAllValues()
    {
        // v1.6.0 wrote the settings class as is into "ExcelMapping" and had no "Format"
        var settings = CustomSettings();
        string json = $$"""{ "Name": "Legacy", "ExcelMapping": {{JsonSerializer.Serialize(settings)}} }""";
        var dto = JsonSerializer.Deserialize<MappingProfileDto>(json)!;

        var profile = _serializer.FromDto(dto);

        profile.Name.Should().Be("Legacy");
        profile.FormatId.Should().Be(PeakmileFormatAdapter.Id);
        profile.Peakmile().Should().BeEquivalentTo(settings);
    }

    [Test]
    public void FromDto_WhenProfileUsesOldPropertyNames_ShouldMapThem()
    {
        // Versions before 1.5.0 stored the order and customer number settings as "...Id"
        string json = """
            { "Name": "Old", "ExcelMapping": { "Header": {
                "EnableOrderId": true, "OrderIdCell": "G1", "EnableCustomerId": true, "CustomerIdCell": "G2" } } }
            """;
        var dto = JsonSerializer.Deserialize<MappingProfileDto>(json)!;

        var header = _serializer.FromDto(dto).Peakmile().Header;

        header.EnableOrderNumber.Should().BeTrue();
        header.OrderNumberCell.Should().Be("G1");
        header.EnableCustomerNumber.Should().BeTrue();
        header.CustomerNumberCell.Should().Be("G2");
    }

    [Test]
    public void FromDto_WhenNoSettingsAreStored_ShouldUseFormatDefaults()
    {
        var profile = _serializer.FromDto(new MappingProfileDto { Name = "Default" });

        profile.FormatId.Should().Be(PeakmileFormatAdapter.Id);
        profile.Settings.Should().BeEquivalentTo(new PeakmileSettings());
    }

    [Test]
    public void FromDto_WhenFormatIsNotRegistered_ShouldKeepTheRawSettings()
    {
        var raw = JsonDocument.Parse("""{ "EanColumn": "A" }""").RootElement;

        var profile = _serializer.FromDto(new MappingProfileDto { Name = "Future", Format = "future-format", Settings = raw });

        profile.FormatId.Should().Be("future-format");
        profile.Settings.Should().BeNull();
        profile.RawSettings!.Value.GetRawText().Should().Be(raw.GetRawText());
    }

    [Test]
    public void ToDto_WhenFormatIsNotRegistered_ShouldWriteTheRawSettingsBack()
    {
        var raw = JsonDocument.Parse("""{ "EanColumn": "A" }""").RootElement;
        var profile = _serializer.FromDto(new MappingProfileDto { Name = "Future", Format = "future-format", Settings = raw });

        var dto = _serializer.ToDto(profile);

        dto.Format.Should().Be("future-format");
        dto.Settings!.Value.GetRawText().Should().Be(raw.GetRawText());
    }

    [Test]
    public void ToDto_ShouldWriteFormatAndSettingsWithoutLegacyKey()
    {
        var profile = ProfileTestExtensions.PeakmileProfile("Custom", CustomSettings());

        string json = JsonSerializer.Serialize(_serializer.ToDto(profile));

        json.Should().Contain("\"Format\":\"peakmile\"").And.Contain("\"Settings\":").And.NotContain("ExcelMapping");
    }

    [Test]
    public void ToDtoAndFromDto_ShouldRoundTripTheProfile()
    {
        var profile = ProfileTestExtensions.PeakmileProfile("Custom", CustomSettings());

        string json = JsonSerializer.Serialize(_serializer.ToDto(profile));
        var restored = _serializer.FromDto(JsonSerializer.Deserialize<MappingProfileDto>(json)!);

        restored.Name.Should().Be("Custom");
        restored.FormatId.Should().Be(PeakmileFormatAdapter.Id);
        restored.Peakmile().Should().BeEquivalentTo(profile.Peakmile());
    }

    [Test]
    public void Constructor_WhenRegistryIsNull_ShouldThrow()
    {
        Action act = () => _ = new ProfileSerializer(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void FromDtoAndToDto_WhenArgumentIsNull_ShouldThrow()
    {
        Action fromDto = () => _serializer.FromDto(null!);
        Action toDto = () => _serializer.ToDto(null!);

        fromDto.Should().Throw<ArgumentNullException>();
        toDto.Should().Throw<ArgumentNullException>();
    }
}
