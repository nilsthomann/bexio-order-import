using System.Text.Json;
using FluentAssertions;

namespace BexioOrderImport.Format.Peakmile.Tests;

public class PeakmileSettingsTests
{
    [Test]
    public void JsonRoundTrip_WhenGrouped_ShouldKeepBothTemplates()
    {
        var settings = new PeakmileSettings
        {
            PositionGroupingMode = PositionGroupingMode.GroupedSizePosition,
            SinglePositionTextTemplate = "single {Size}",
            GroupedPositionTextTemplate = "grouped {SizesRows}"
        };

        var restored = JsonSerializer.Deserialize<PeakmileSettings>(JsonSerializer.Serialize(settings))!;

        restored.SinglePositionTextTemplate.Should().Be("single {Size}");
        restored.GroupedPositionTextTemplate.Should().Be("grouped {SizesRows}");
        restored.PositionTextTemplate.Should().Be("grouped {SizesRows}");
    }

    [Test]
    public void Serialize_ShouldNotWriteTheComputedPositionTextTemplate()
    {
        string json = JsonSerializer.Serialize(new PeakmileSettings());

        json.Should().NotContain("\"PositionTextTemplate\"");
    }

    [Test]
    public void Deserialize_WhenSavedByVersion16InGroupedMode_ShouldKeepTheSingleTemplate()
    {
        // v1.6.0 wrote the computed "PositionTextTemplate" (here the grouped one) before the grouping mode
        string json = """
            {
              "SinglePositionTextTemplate": "single {Size}",
              "GroupedPositionTextTemplate": "grouped {SizesRows}",
              "PositionTextTemplate": "grouped {SizesRows}",
              "PositionGroupingMode": 1
            }
            """;

        var settings = JsonSerializer.Deserialize<PeakmileSettings>(json)!;

        settings.SinglePositionTextTemplate.Should().Be("single {Size}");
        settings.GroupedPositionTextTemplate.Should().Be("grouped {SizesRows}");
    }

    [Test]
    public void Deserialize_WhenSavedByVersion13_ShouldUsePositionTextTemplateAsSingleTemplate()
    {
        // Up to v1.3.0 there was only one template
        var settings = JsonSerializer.Deserialize<PeakmileSettings>("""{ "PositionTextTemplate": "old {Size}" }""")!;

        settings.SinglePositionTextTemplate.Should().Be("old {Size}");
        settings.GroupedPositionTextTemplate.Should().Be(new PeakmileSettings().GroupedPositionTextTemplate);
    }

    [Test]
    [Arguments(PositionGroupingMode.SinglePositionPerSize, "single")]
    [Arguments(PositionGroupingMode.GroupedSizePosition, "grouped")]
    public void PositionTextTemplate_ShouldBeTheTemplateOfTheGroupingMode(PositionGroupingMode mode, string expected)
    {
        var settings = new PeakmileSettings
        {
            SinglePositionTextTemplate = "single",
            GroupedPositionTextTemplate = "grouped",
            PositionGroupingMode = mode
        };

        settings.PositionTextTemplate.Should().Be(expected);
    }

    [Test]
    public void Serialize_ShouldNotWriteTheOldHeaderNames()
    {
        var settings = new PeakmileSettings();
        settings.Header.EnableOrderNumber = true;
        settings.Header.OrderNumberCell = "G1";

        string json = JsonSerializer.Serialize(settings);

        json.Should().Contain("\"OrderNumberCell\":\"G1\"").And.NotContain("OrderIdCell").And.NotContain("EnableOrderId")
            .And.NotContain("EnableCustomerId").And.NotContain("CustomerIdCell");
        JsonSerializer.Deserialize<PeakmileSettings>(json)!.Header.OrderNumberCell.Should().Be("G1");
    }
}
