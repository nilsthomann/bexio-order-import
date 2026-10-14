using System.Text.Json;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Peakmile;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class PeakmileFormatAdapterTests
{
    private sealed class OtherSettings : IFormatSettings
    {
    }

    [Test]
    public void Metadata_ShouldDescribeTheFormat()
    {
        var adapter = new PeakmileFormatAdapter();

        adapter.FormatId.Should().Be("peakmile");
        adapter.DisplayName.Should().NotBeNullOrWhiteSpace();
        adapter.Description.Should().NotBeNullOrWhiteSpace();
        adapter.Version.Should().Be(typeof(PeakmileFormatAdapter).Assembly.GetName().Version);
    }

    [Test]
    public void CreateDefaultSettings_ShouldReturnNewPeakmileSettings()
    {
        var adapter = new PeakmileFormatAdapter();

        var settings = adapter.CreateDefaultSettings();

        settings.Should().BeOfType<PeakmileSettings>().Which.Should().BeEquivalentTo(new PeakmileSettings());
    }

    [Test]
    public void SerializeAndDeserializeSettings_ShouldRoundTripAllValues()
    {
        var adapter = new PeakmileFormatAdapter();
        var settings = new PeakmileSettings
        {
            WorksheetIndex = 3,
            SeasonCode = "HW26",
            SeasonFallbackCodes = ["NOOS", "EX"],
            PositionGroupingMode = PositionGroupingMode.SinglePositionPerSize,
            Header = new HeaderMapping { CompanyNameCell = "C3", EnableCustomerNumber = true },
            Data = new DataMapping { UnitPriceColumn = "X", EnableRowDiscount = true }
        };

        var json = adapter.SerializeSettings(settings);
        var restored = adapter.DeserializeSettings(json);

        restored.Should().BeEquivalentTo(settings);
    }

    [Test]
    public void DeserializeSettings_WhenJsonIsNull_ShouldReturnDefaults()
    {
        var adapter = new PeakmileFormatAdapter();

        var settings = adapter.DeserializeSettings(JsonDocument.Parse("null").RootElement);

        settings.Should().BeEquivalentTo(new PeakmileSettings());
    }

    [Test]
    public void CloneSettings_ShouldReturnIndependentCopy()
    {
        var adapter = new PeakmileFormatAdapter();
        var settings = new PeakmileSettings { Header = new HeaderMapping { CompanyNameCell = "A1" } };

        var clone = (PeakmileSettings)adapter.CloneSettings(settings);
        clone.Header.CompanyNameCell = "Z9";

        clone.Should().NotBeSameAs(settings);
        settings.Header.CompanyNameCell.Should().Be("A1");
    }

    [Test]
    public void Parse_ShouldUseParserCreatedWithTheSettings()
    {
        var order = new Order();
        var settings = new PeakmileSettings();
        PeakmileSettings? usedSettings = null;
        var parser = new Mock<IExcelParser>();
        parser.Setup(p => p.ParseOrderForm("order.xlsx")).Returns(order);
        var adapter = new PeakmileFormatAdapter(s => { usedSettings = s; return parser.Object; });

        var result = adapter.Parse("order.xlsx", settings);

        result.Should().BeSameAs(order);
        usedSettings.Should().BeSameAs(settings);
    }

    [Test]
    public void Parse_WhenSettingsAreOfAnotherFormat_ShouldThrow()
    {
        var adapter = new PeakmileFormatAdapter();

        Action act = () => adapter.Parse("order.xlsx", new OtherSettings());

        act.Should().Throw<ArgumentException>().WithMessage("*PeakmileSettings*OtherSettings*");
    }

    [Test]
    public void Parse_WhenSettingsAreNull_ShouldThrow()
    {
        var adapter = new PeakmileFormatAdapter();

        Action act = () => adapter.Parse("order.xlsx", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WhenParserFactoryIsNull_ShouldThrow()
    {
        Action act = () => _ = new PeakmileFormatAdapter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public async Task ImportAsync_ShouldRunThePeakmileImport()
    {
        var adapter = new PeakmileFormatAdapter();
        var bexio = new Mock<IBexioService>();
        var logs = new List<string>();
        var interaction = new DelegateImportUserInteractionService(logInfo: logs.Add);

        var result = await adapter.ImportAsync(new Order(), new PeakmileSettings(), bexio.Object, interaction);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No positions found.");
        logs.Should().Contain("No order positions with quantity > 0 found.");
    }

    [Test]
    public void CreateImportOptions_ShouldMapTheProfileSettings()
    {
        var settings = new PeakmileSettings
        {
            DefaultOrderName = "Order {CustomerName}",
            SeasonCode = "HW26",
            SeasonFallbackCodes = ["NOOS"],
            PositionGroupingMode = PositionGroupingMode.GroupedSizePosition,
            GroupedPositionTextTemplate = "{SizesRows}",
            DiscountPositionTextTemplate = "Discount {DiscountInPercent}",
            SizeRowTemplate = "{Amount} x {Size}"
        };

        var options = PeakmileFormatAdapter.CreateImportOptions(settings);

        options.Should().BeEquivalentTo(new PeakmileImportOptions(
            DefaultOrderName: "Order {CustomerName}",
            SeasonCode: "HW26",
            PositionTextTemplate: "{SizesRows}",
            DiscountPositionTextTemplate: "Discount {DiscountInPercent}",
            SizeRowTemplate: "{Amount} x {Size}",
            SeasonFallbackCodes: ["NOOS"]));
    }
}
