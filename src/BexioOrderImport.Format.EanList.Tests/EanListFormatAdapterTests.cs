using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Format.EanList.Tests;

public class EanListFormatAdapterTests
{
    [Test]
    public void Metadata_ShouldDescribeTheFormat()
    {
        var adapter = new EanListFormatAdapter();

        adapter.FormatId.Should().Be("ean-list");
        adapter.DisplayName.Should().NotBeNullOrWhiteSpace();
        adapter.Description.Should().NotBeNullOrWhiteSpace();
        adapter.CreateDefaultSettings().Should().BeOfType<EanListSettings>();
    }

    [Test]
    public void Parse_ShouldReadTheEanList()
    {
        var order = new EanListFormatAdapter().Parse(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EanListOrder.xlsx"), new EanListSettings());

        order.CustomerNumber.Should().Be("1001");
        order.Positions.Should().HaveCount(4);
    }

    [Test]
    public async Task ImportAsync_ShouldRunTheEanListImport()
    {
        var result = await new EanListFormatAdapter().ImportAsync(new Order(), new EanListSettings(), Mock.Of<IBexioService>(), new DelegateImportUserInteractionService());

        result.ErrorMessage.Should().Be("Customer number missing.");
    }

    [Test]
    public void SettingsJson_ShouldRoundTripAndNormalizeColumns()
    {
        var adapter = new EanListFormatAdapter();
        var settings = new EanListSettings { WorksheetIndex = 2, StartRow = 4, EanColumn = "C", QuantityColumn = "D", CustomerNumberColumn = "E", OrderNumberColumn = "F", DefaultOrderName = "X", PositionTextTemplate = "{Ean}" };

        var restored = adapter.DeserializeSettings(adapter.SerializeSettings(settings));

        restored.Should().BeEquivalentTo(settings);
    }
}
