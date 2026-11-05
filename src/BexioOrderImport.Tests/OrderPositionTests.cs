using BexioOrderImport.Domain.Models;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class OrderPositionTests
{
    [Test]
    public void OrderPosition_WithoutDiscount_ShouldCalculateNetAndTotalFromGrossUnitPrice()
    {
        // Arrange & Act
        var pos = new OrderPosition
        {
            GrossUnitPrice = 100m,
            DiscountPercent = null,
            Quantity = 5
        };

        // Assert
        pos.NetUnitPrice.Should().Be(100m);
        pos.TotalPrice.Should().Be(500m);
    }

    [Test]
    public void OrderPosition_WithDiscount_ShouldCalculateNetUnitPriceAndTotalPriceCorrectly()
    {
        // Arrange & Act
        var pos = new OrderPosition
        {
            GrossUnitPrice = 100m,
            DiscountPercent = 15m, // 15% discount
            Quantity = 4
        };

        // Assert
        pos.NetUnitPrice.Should().Be(85m);
        pos.TotalPrice.Should().Be(340m);
    }

    [Test]
    public void OrderPosition_WithDecimalDiscount_ShouldRoundNetUnitPriceToOneDecimalPlace()
    {
        // Arrange & Act
        var pos1 = new OrderPosition
        {
            GrossUnitPrice = 36.36m,
            DiscountPercent = 12m, // 36.36 * 0.88 = 31.9968 -> RoundUp to 1 decimal place = 32.0m
            Quantity = 1
        };

        var pos2 = new OrderPosition
        {
            GrossUnitPrice = 36.47m,
            DiscountPercent = 12m, // 36.47 * 0.88 = 32.0936 -> 32.1m
            Quantity = 1
        };

        // Assert
        pos1.NetUnitPrice.Should().Be(32.0m);
        pos2.NetUnitPrice.Should().Be(32.1m);
    }
}
