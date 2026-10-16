using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class OrderTargetResolverTests
{
    private readonly Mock<IBexioService> _bexio = new();
    private readonly IImportUserInteractionService _interaction = new DelegateImportUserInteractionService();

    [Test]
    public async Task ResolveAsync_WithoutOrderAndCustomerNumber_ShouldLeaveItToTheFormat()
    {
        var target = await OrderTargetResolver.ResolveAsync(new Order(), "{CustomerName}", "FS27", _bexio.Object, _interaction);

        target.Should().BeNull();
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ResolveAsync_WithCustomerNumber_ShouldCreateTheOrderWithTheTitle()
    {
        _bexio.Setup(b => b.FindContactByNrAsync("1001")).ReturnsAsync(new BexioContact { Id = 5, Name = "Bexio Name" });
        _bexio.Setup(b => b.CreateOrderAsync(5, It.IsAny<Order>())).ReturnsAsync(new BexioOrder { Id = 7, DocumentNr = "AU-7" });
        var order = new Order { CustomerNumber = "1001", Customer = new Customer { CompanyName = "Excel Name" } };

        var target = await OrderTargetResolver.ResolveAsync(order, "Order {CustomerName} {SeasonCode}", "FS27", _bexio.Object, _interaction, useContactNameAsCustomerName: true);

        target.Should().Be(new OrderTarget(7, "AU-7"));
        order.Title.Should().Be("Order Excel Name FS27", "a customer name from the file is kept");
    }

    [Test]
    public async Task ResolveAsync_WhenCustomerNameIsEmpty_ShouldUseTheContactNameOnlyIfAsked()
    {
        _bexio.Setup(b => b.FindContactByNrAsync("1001")).ReturnsAsync(new BexioContact { Id = 5, Name = "Bexio Name" });
        _bexio.Setup(b => b.CreateOrderAsync(5, It.IsAny<Order>())).ReturnsAsync(new BexioOrder { Id = 7, DocumentNr = "AU-7" });
        var withContactName = new Order { CustomerNumber = "1001" };
        var withoutContactName = new Order { CustomerNumber = "1001" };

        await OrderTargetResolver.ResolveAsync(withContactName, "{CustomerName}", "", _bexio.Object, _interaction, useContactNameAsCustomerName: true);
        await OrderTargetResolver.ResolveAsync(withoutContactName, "{CustomerName}", "", _bexio.Object, _interaction);

        withContactName.Title.Should().Be("Bexio Name");
        withoutContactName.Title.Should().BeEmpty();
    }

    [Test]
    public async Task ResolveAsync_WithOrderNumberAndNoCustomerNumber_ShouldAskTheFormatToCheckTheContact()
    {
        var contact = new BexioContact { Id = 5 };
        _bexio.Setup(b => b.FindOrderByDocumentNrAsync("AU-1")).ReturnsAsync(new BexioOrder { Id = 40, DocumentNr = "AU-1", ContactId = 5 });
        _bexio.Setup(b => b.GetContactDetailsAsync(5)).ReturnsAsync(contact);
        BexioContact? checkedContact = null;
        var failure = new ImportResult(false, ErrorMessage: "wrong contact");

        var target = await OrderTargetResolver.ResolveAsync(
            new Order { OrderNumber = "AU-1" }, "", "", _bexio.Object, _interaction,
            checkContactWithoutCustomerNumber: c => { checkedContact = c; return Task.FromResult<ImportResult?>(failure); });

        checkedContact.Should().BeSameAs(contact);
        target!.Failure.Should().BeSameAs(failure);
    }

    [Test]
    public async Task ResolveAsync_WithOrderNumberAndNoCheck_ShouldUseTheExistingOrder()
    {
        _bexio.Setup(b => b.FindOrderByDocumentNrAsync("AU-1")).ReturnsAsync(new BexioOrder { Id = 40, DocumentNr = "AU-1", ContactId = 5 });

        var target = await OrderTargetResolver.ResolveAsync(new Order { OrderNumber = "AU-1" }, "", "", _bexio.Object, _interaction);

        target.Should().Be(new OrderTarget(40, "AU-1"));
    }

    [Test]
    public async Task ResolveAsync_WhenArgumentIsNull_ShouldThrow()
    {
        await ((Func<Task>)(() => OrderTargetResolver.ResolveAsync(null!, "", "", _bexio.Object, _interaction))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => OrderTargetResolver.ResolveAsync(new Order(), "", "", null!, _interaction))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => OrderTargetResolver.ResolveAsync(new Order(), "", "", _bexio.Object, null!))).Should().ThrowAsync<ArgumentNullException>();
    }
}
