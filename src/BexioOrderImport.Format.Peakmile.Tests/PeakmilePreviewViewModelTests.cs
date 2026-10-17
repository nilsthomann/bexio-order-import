using BexioOrderImport.Domain.Models;
using FluentAssertions;

namespace BexioOrderImport.Format.Peakmile.Tests;

public class PeakmilePreviewViewModelTests
{
    [Test]
    public void Constructor_ShouldShowHeaderPositionsAndTotals()
    {
        var order = new Order
        {
            Customer = new Customer { CompanyName = "Muster AG", BuyerName = "Hans", Email = "hans@muster.ch", Street = "Gasse 1", ZipCode = "8000", City = "Zürich" },
            OrderNumber = "AU-1",
            CustomerNumber = "1001",
            PaymentTerms = "30 Tage",
            DiscountPercent = 10,
            Positions =
            [
                new OrderPosition { Quantity = 2, GrossUnitPrice = 10m },
                new OrderPosition { Quantity = 3, GrossUnitPrice = 20m }
            ]
        };

        var preview = new PeakmilePreviewViewModel(order, new PeakmileSettings { Data = new DataMapping { EnableRowDiscount = true } });

        preview.CompanyName.Should().Be("Muster AG");
        preview.BuyerName.Should().Be("Hans");
        preview.Email.Should().Be("hans@muster.ch");
        preview.Address.Should().Be("Gasse 1, 8000 Zürich");
        preview.OrderNumber.Should().Be("AU-1");
        preview.CustomerNumber.Should().Be("1001");
        preview.PaymentTerms.Should().Be("30 Tage");
        preview.Positions.Should().BeSameAs(order.Positions);
        preview.IsRowDiscountEnabled.Should().BeTrue();
        preview.TotalQuantity.Should().Be(5);
        preview.TotalGrossAmount.Should().Be(80m);
        preview.DiscountPercent.Should().Be(10m);
        preview.TotalNetAmount.Should().Be(72m);
    }

    [Test]
    public void Constructor_WhenHeaderIsEmpty_ShouldShowDashes()
    {
        var preview = new PeakmilePreviewViewModel(new Order(), new PeakmileSettings());

        preview.CompanyName.Should().Be("-");
        preview.BuyerName.Should().Be("-");
        preview.Email.Should().Be("-");
        preview.Address.Should().Be("-");
        preview.OrderNumber.Should().Be("-");
        preview.CustomerNumber.Should().Be("-");
        preview.PaymentTerms.Should().Be("-");
        preview.IsRowDiscountEnabled.Should().BeFalse();
        preview.TotalNetAmount.Should().Be(0m);
    }

    [Test]
    public void Constructor_WhenArgumentIsNull_ShouldThrow()
    {
        Action withoutOrder = () => _ = new PeakmilePreviewViewModel(null!, new PeakmileSettings());
        Action withoutSettings = () => _ = new PeakmilePreviewViewModel(new Order(), null!);

        withoutOrder.Should().Throw<ArgumentNullException>();
        withoutSettings.Should().Throw<ArgumentNullException>();
    }
}
