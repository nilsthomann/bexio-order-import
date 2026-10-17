using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Format.EanList.Tests;

public class EanListImporterTests
{
    private readonly Mock<IBexioService> _bexio = new();
    private readonly List<string> _logs = [];
    private readonly EanListSettings _settings = new() { DefaultOrderName = "Order: {CustomerName}" };

    private DelegateImportUserInteractionService Interaction(bool confirmUpload = true) => new DelegateImportUserInteractionService(
        confirmUpload: () => Task.FromResult(confirmUpload),
        logInfo: _logs.Add);

    private static Order Order(string? customerNumber = "1001", string? orderNumber = null, params string[] eans) => new()
    {
        CustomerNumber = customerNumber,
        OrderNumber = orderNumber,
        Positions = [.. (eans.Length == 0 ? ["4006381333931"] : eans).Select((ean, i) => new OrderPosition { Ean = ean, Quantity = i + 1, SourceRow = i + 2 })]
    };

    private static BexioArticle Article(int id, decimal? price = 12.5m) => new()
    {
        Id = id, Code = $"code{id}", Name = $"Article {id}", Description = $"Description {id}", SalePrice = price
    };

    private void SetupNewOrder()
    {
        _bexio.Setup(b => b.FindContactByNrAsync("1001")).ReturnsAsync(new BexioContact { Id = 5, Nr = "1001", Name = "Muster AG" });
        _bexio.Setup(b => b.CreateOrderAsync(5, It.IsAny<Order>())).ReturnsAsync(new BexioOrder { Id = 77, DocumentNr = "AU-77" });
    }

    private void SetupArticle(string ean, params BexioArticle[] articles)
        => _bexio.Setup(b => b.FindArticlesByInternCodeAsync(ean)).ReturnsAsync(articles);

    [Test]
    [Arguments(null, null)]
    [Arguments(" ", "AU-1")]
    public async Task ExecuteAsync_WhenCustomerNumberIsMissing_ShouldCancelWithoutBexioCalls(string? customerNumber, string? orderNumber)
    {
        bool asked = false;
        var interaction = new DelegateImportUserInteractionService(confirmUpload: () => { asked = true; return Task.FromResult(true); }, logInfo: _logs.Add);

        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(customerNumber, orderNumber), _settings, interaction);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Customer number missing.");
        asked.Should().BeFalse();
        _logs.Should().Contain(l => l.Contains("row 2"));
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ExecuteAsync_WhenThereAreNoPositions_ShouldFail()
    {
        var order = new Order { CustomerNumber = "1001" };

        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(order, _settings, Interaction());

        result.ErrorMessage.Should().Be("No positions found.");
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ExecuteAsync_WhenUploadIsNotConfirmed_ShouldCancel()
    {
        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(), _settings, Interaction(confirmUpload: false));

        result.ErrorMessage.Should().Be("Cancelled by user.");
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ExecuteAsync_WhenCustomerIsNotFound_ShouldFailWithoutCreatingAnything()
    {
        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(), _settings, Interaction());

        result.ErrorMessage.Should().Be("Customer 1001 not found.");
        _bexio.Verify(b => b.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
        _bexio.Verify(b => b.CreateContactAsync(It.IsAny<Customer>()), Times.Never);
        _bexio.Verify(b => b.FindContactByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldCreateTheOrderAndAddOnePositionPerRowWithTheBexioPrice()
    {
        SetupNewOrder();
        SetupArticle("4006381333931", Article(1));
        SetupArticle("7612345678904", Article(2, 30m));
        var order = Order(eans: ["4006381333931", "7612345678904", "4006381333931"]);
        var settings = new EanListSettings { PositionTextTemplate = "{Ean}: {BexioArticleName} - {BexioArticleDescription}" };

        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(order, settings, Interaction());

        result.Should().Be(new ImportResult(Success: true, OrderNumber: "AU-77", UploadedPositionsCount: 3));
        order.Title.Should().Be("Order: Muster AG", "the customer name comes from the Bexio contact");
        order.Positions.Select(p => p.GrossUnitPrice).Should().Equal(12.5m, 30m, 12.5m);
        _bexio.Verify(b => b.AddArticlePositionAsync(77, 1, It.IsAny<OrderPosition>(), "4006381333931: Article 1 - Description 1"), Times.Exactly(2));
        _bexio.Verify(b => b.AddArticlePositionAsync(77, 2, It.Is<OrderPosition>(p => p.Quantity == 2), "7612345678904: Article 2 - Description 2"), Times.Once);
        _bexio.Verify(b => b.AddDiscountPositionAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumber_ShouldAddThePositionsToTheExistingOrder()
    {
        _bexio.Setup(b => b.FindOrderByDocumentNrAsync("AU-1")).ReturnsAsync(new BexioOrder { Id = 40, DocumentNr = "AU-1", ContactId = 5 });
        _bexio.Setup(b => b.GetContactDetailsAsync(5)).ReturnsAsync(new BexioContact { Id = 5, Nr = "1001" });
        SetupArticle("4006381333931", Article(1));

        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(orderNumber: "AU-1"), _settings, Interaction());

        result.Success.Should().BeTrue();
        result.OrderNumber.Should().Be("AU-1");
        _bexio.Verify(b => b.AddArticlePositionAsync(40, 1, It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Once);
        _bexio.Verify(b => b.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenExistingOrderBelongsToAnotherCustomer_ShouldFail()
    {
        _bexio.Setup(b => b.FindOrderByDocumentNrAsync("AU-1")).ReturnsAsync(new BexioOrder { Id = 40, DocumentNr = "AU-1", ContactId = 6 });
        _bexio.Setup(b => b.GetContactDetailsAsync(6)).ReturnsAsync(new BexioContact { Id = 6, Nr = "2002" });

        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(orderNumber: "AU-1"), _settings, Interaction());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mismatch");
        _bexio.Verify(b => b.AddArticlePositionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenExistingOrderIsNotFound_ShouldFail()
    {
        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(orderNumber: "AU-1"), _settings, Interaction());

        result.ErrorMessage.Should().Be("Order AU-1 not found.");
    }

    [Test]
    public async Task ExecuteAsync_WhenEanIsNotFound_ShouldAbortWithEanAndRow()
    {
        SetupNewOrder();
        SetupArticle("4006381333931");
        _bexio.Setup(b => b.SearchArticlesAsync(It.IsAny<IReadOnlyList<BexioSearchCriteria>>())).ReturnsAsync([]);

        Func<Task> act = () => new EanListImporter(_bexio.Object).ExecuteAsync(Order(), _settings, Interaction());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*4006381333931*row 2*not found*");
    }

    [Test]
    public async Task ExecuteAsync_WhenTheExcelCellLostTheLeadingZero_ShouldFindTheArticle()
    {
        SetupNewOrder();
        SetupArticle("826710896036");
        var article = Article(3);
        article.Code = "0826710896036";
        _bexio.Setup(b => b.SearchArticlesAsync(It.IsAny<IReadOnlyList<BexioSearchCriteria>>())).ReturnsAsync([article]);

        var result = await new EanListImporter(_bexio.Object).ExecuteAsync(Order(eans: ["826710896036"]), _settings, Interaction());

        result.Success.Should().BeTrue();
        _bexio.Verify(b => b.AddArticlePositionAsync(77, 3, It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenEanMatchesSeveralArticles_ShouldAbort()
    {
        SetupNewOrder();
        SetupArticle("4006381333931", Article(1), Article(2));

        Func<Task> act = () => new EanListImporter(_bexio.Object).ExecuteAsync(Order(), _settings, Interaction());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not unique (2 articles)*");
        _bexio.Verify(b => b.AddArticlePositionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithArticleMatcher_ShouldUseItAndAbortWithItsReason()
    {
        SetupNewOrder();
        var matcher = new Mock<IArticleMatcher<EanArticleCriteria>>();
        matcher.Setup(m => m.FindArticleAsync(new EanArticleCriteria("7612345678904"))).ReturnsAsync(ArticleMatch.Found(Article(2)));
        matcher.Setup(m => m.FindArticleAsync(new EanArticleCriteria("4006381333931"))).ReturnsAsync(ArticleMatch.NotFound("blocked"));

        Func<Task> act = () => new EanListImporter(_bexio.Object, matcher.Object).ExecuteAsync(Order(eans: ["7612345678904", "4006381333931"]), _settings, Interaction());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("EAN '4006381333931' (row 3) blocked in Bexio.");
        _bexio.Verify(b => b.AddArticlePositionAsync(77, 2, It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Once);
        _bexio.Verify(b => b.FindArticlesByInternCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenArticleHasNoSalePrice_ShouldWarnAndUseZero()
    {
        SetupNewOrder();
        SetupArticle("4006381333931", Article(1, price: null));
        var order = Order();

        await new EanListImporter(_bexio.Object).ExecuteAsync(order, _settings, Interaction());

        order.Positions[0].GrossUnitPrice.Should().Be(0m);
        _logs.Should().Contain(l => l.Contains("no sale price"));
    }

    [Test]
    public async Task ExecuteAsync_WhenArgumentIsNull_ShouldThrow()
    {
        var importer = new EanListImporter(_bexio.Object);

        await ((Func<Task>)(() => importer.ExecuteAsync(null!, _settings, Interaction()))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => importer.ExecuteAsync(Order(), null!, Interaction()))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => importer.ExecuteAsync(Order(), _settings, null!))).Should().ThrowAsync<ArgumentNullException>();
        ((Action)(() => _ = new EanListImporter(null!))).Should().Throw<ArgumentNullException>();
    }
}
