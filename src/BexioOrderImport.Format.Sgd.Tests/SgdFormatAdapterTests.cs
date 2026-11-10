using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Format.Sgd.Tests;

public class SgdFormatAdapterTests
{
    [Test]
    public void Metadata_ShouldDescribeTheFormat()
    {
        var adapter = new SgdFormatAdapter();

        adapter.FormatId.Should().Be("sgd");
        adapter.DisplayName.Should().Contain("SGD");
        adapter.Description.Should().NotBeNullOrWhiteSpace();
        adapter.CreateDefaultSettings().Should().BeOfType<SgdSettings>();
    }

    [Test]
    public void Parse_ShouldReadTheSgdList()
    {
        var order = new SgdFormatAdapter().Parse(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SgdOrder.xlsx"), Utils.SgdTestSettings.WithSeasons());

        order.Positions.Should().HaveCount(6);
    }

    [Test]
    public async Task ImportAsync_ShouldRunTheSgdImport()
    {
        var result = await new SgdFormatAdapter().ImportAsync(new Order(), new SgdSettings(), Mock.Of<IBexioService>(), new DelegateImportUserInteractionService());

        result.ErrorMessage.Should().Be("No positions found.");
    }

    [Test]
    public void SettingsJson_ShouldRoundTrip()
    {
        var adapter = new SgdFormatAdapter();
        var settings = new SgdSettings
        {
            WorksheetIndex = 2, StartRow = 4,
            ArticleNumberColumn = "F", ArticleNameColumn = "G", ColorColumn = "H", SizeColumn = "I", QuantityColumn = "J",
            SeasonColumn = "K", SeasonMappings = [new SgdSeasonMapping { ExternalSeason = "2027 Spring/Summer", SeasonCode = "FS27" }], DiscountPercent = 12.5m,
            PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition,
            DefaultOrderName = "X", SinglePositionTextTemplate = "S", GroupedPositionTextTemplate = "{SizesRows}",
            SizeRowTemplate = "{Amount}/{Size}", DiscountPositionTextTemplate = "D"
        };

        var restored = adapter.DeserializeSettings(adapter.SerializeSettings(settings));

        restored.Should().BeEquivalentTo(settings);
    }

    [Test]
    public void DeserializeSettings_WithAnEmptyProfile_ShouldUseTheDefaults()
    {
        var restored = (SgdSettings)new SgdFormatAdapter().DeserializeSettings(System.Text.Json.JsonDocument.Parse("{}").RootElement);

        restored.Should().BeEquivalentTo(new SgdSettings());
    }

    [Test]
    public void DeserializeSettings_WithTheSeasonCodesOfAnOlderVersion_ShouldIgnoreThem()
    {
        var json = System.Text.Json.JsonDocument.Parse("""{ "SeasonCode": "FS27", "SeasonFallbackCodes": ["NOOS"], "StartRow": 3 }""").RootElement;

        var restored = (SgdSettings)new SgdFormatAdapter().DeserializeSettings(json);

        restored.StartRow.Should().Be(3);
        restored.SeasonMappings.Should().BeEmpty("the SGD season names are not known, the parser reports them");
    }
}
