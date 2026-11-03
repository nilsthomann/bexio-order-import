using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Format.Sgd.Resources;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Format.Sgd.Tests;

public class SgdImporterTests
{
    private readonly Mock<IBexioService> _bexio = new();
    private readonly Mock<ISgdArticleMatcher> _matcher = new();
    private readonly List<string> _logs = [];
    private readonly List<string> _calls = [];
    private readonly SgdSettings _settings = new() { SeasonCode = "FS27", SinglePositionTextTemplate = "{BexioArticleName} {Size} ({Color})" };

    private readonly Queue<string?> _texts = new();
    private static readonly string[] ArticleNumbers = ["E1", "E2"];
    private static readonly string[] FallbackCodes = ["NOOS"];

    private int? _choice = 0;
    private readonly List<(string Title, string? Error)> _askedTexts = [];
    private string? _warning;
    private bool _positionsDeleted;

    private SgdImporter Importer() => new(_bexio.Object, _matcher.Object);

    private DelegateImportUserInteractionService Interaction(bool cancelChoice = false, bool acceptWarning = true) => new(
        logInfo: _logs.Add,
        askText: (title, message, error) =>
        {
            _askedTexts.Add((title, error));
            return Task.FromResult(_texts.Count > 0 ? _texts.Dequeue() : null);
        },
        choose: (title, message, options) =>
        {
            options.Should().Equal(SgdTranslations.Import_NewOrder, SgdTranslations.Import_ExistingOrder);
            message.Should().Contain("Muster Sport AG");
            return Task.FromResult(cancelChoice ? null : _choice);
        },
        confirmWarning: (title, message) =>
        {
            _warning = message;
            return Task.FromResult(acceptWarning);
        },
        orderPositionsDeleted: () => _positionsDeleted = true);

    private static Order Order() => new()
    {
        Positions =
        [
            new OrderPosition { ArticleNumber = "E1", ArticleName = "Jacket", Color = "1001 Black", Size = "M", Quantity = 2, SourceRow = 2 },
            new OrderPosition { ArticleNumber = "E2", ArticleName = "Fleece", Color = "Red", Size = "L", Quantity = 1, SourceRow = 3 }
        ]
    };

    private static BexioArticle Article(int id, decimal? price = 50m) => new()
    {
        Id = id, Code = $"FS27E{id}", Name = $"Article {id}", Description = $"Description {id}", SalePrice = price
    };

    private void SetupCustomer()
        => _bexio.Setup(b => b.FindContactByNrAsync("1001")).ReturnsAsync(new BexioContact { Id = 5, Nr = "1001", Name = "Muster Sport AG" });

    private void SetupArticles(decimal? price = 50m)
    {
        _matcher.Setup(m => m.FindArticleAsync(It.Is<SgdArticleCriteria>(c => c.ArticleNumber == "E1"))).ReturnsAsync(ArticleMatch.Found(Article(1, price)));
        _matcher.Setup(m => m.FindArticleAsync(It.Is<SgdArticleCriteria>(c => c.ArticleNumber == "E2"))).ReturnsAsync(ArticleMatch.Found(Article(2, price)));
    }

    private void SetupNewOrder()
    {
        SetupCustomer();
        SetupArticles();
        _texts.Enqueue("1001");
        _bexio.Setup(b => b.CreateOrderAsync(5, It.IsAny<Order>())).Callback(() => _calls.Add("create")).ReturnsAsync(new BexioOrder { Id = 77, DocumentNr = "AU-77" });
        _bexio.Setup(b => b.AddArticlePositionAsync(77, It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>())).Callback(() => _calls.Add("add"));
    }

    private void SetupExistingOrder(string contactNr = "1001")
    {
        SetupCustomer();
        SetupArticles();
        _texts.Enqueue("1001");
        _texts.Enqueue("AU-12");
        _choice = 1;
        _bexio.Setup(b => b.FindOrderByDocumentNrAsync("AU-12")).ReturnsAsync(new BexioOrder { Id = 12, DocumentNr = "AU-12", ContactId = 5 });
        _bexio.Setup(b => b.GetContactDetailsAsync(5)).ReturnsAsync(new BexioContact { Id = 5, Nr = contactNr, Name = "Muster Sport AG" });
        _bexio.Setup(b => b.DeleteAllPositionsAsync(12)).Callback(() => _calls.Add("delete")).ReturnsAsync(4);
        _bexio.Setup(b => b.AddArticlePositionAsync(12, It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>())).Callback(() => _calls.Add("add"));
    }

    [Test]
    public async Task ExecuteAsync_WhenThereAreNoPositions_ShouldFailWithoutAsking()
    {
        var result = await Importer().ExecuteAsync(new Order(), _settings, Interaction());

        result.ErrorMessage.Should().Be("No positions found.");
        _askedTexts.Should().BeEmpty();
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ExecuteAsync_WhenTheCustomerDialogIsCancelled_ShouldCancelWithoutBexioCalls()
    {
        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction());

        result.Should().Be(new ImportResult(Success: false, ErrorMessage: "Cancelled by user."));
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ExecuteAsync_ShouldAskAgainUntilTheCustomerIsFound()
    {
        SetupNewOrder();
        _texts.Clear();
        _texts.Enqueue(" ");
        _texts.Enqueue("9999");
        _texts.Enqueue(" 1001 ");

        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction());

        result.Success.Should().BeTrue();
        _askedTexts.Should().Equal(
            (SgdTranslations.Import_CustomerTitle, null),
            (SgdTranslations.Import_CustomerTitle, SgdTranslations.Import_CustomerRequired),
            (SgdTranslations.Import_CustomerTitle, string.Format(SgdTranslations.Import_CustomerNotFound, "9999")));
        _logs.Should().Contain("⛔ Customer #9999 not found in Bexio.");
    }

    [Test]
    public async Task ExecuteAsync_WhenTheOrderChoiceIsCancelled_ShouldCancelWithoutChangingBexio()
    {
        SetupNewOrder();

        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction(cancelChoice: true));

        result.ErrorMessage.Should().Be("Cancelled by user.");
        _calls.Should().BeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_WithANewOrder_ShouldCreateItAndAddThePositionsWithTheBexioPriceAndTheDiscount()
    {
        SetupNewOrder();
        _settings.SeasonFallbackCodes = ["NOOS", " "];
        _settings.DiscountPercent = 7.5m;
        _settings.DiscountPositionTextTemplate = "Rabatt {DiscountInPercent}%";
        var order = Order();

        var result = await Importer().ExecuteAsync(order, _settings, Interaction());

        result.Should().Be(new ImportResult(Success: true, OrderNumber: "AU-77", UploadedPositionsCount: 2));
        order.CustomerNumber.Should().Be("1001");
        order.Title.Should().Be("Order: Muster Sport AG FS27");
        order.Positions.Select(p => p.GrossUnitPrice).Should().Equal(50m, 50m);
        _bexio.Verify(b => b.AddArticlePositionAsync(77, 1, order.Positions[0], "Article 1 M (1001 Black)"), Times.Once);
        _bexio.Verify(b => b.AddArticlePositionAsync(77, 2, order.Positions[1], "Article 2 L (Red)"), Times.Once);
        _bexio.Verify(b => b.AddDiscountPositionAsync(77, 7.5m, "Rabatt 7.5%"), Times.Once);
        _bexio.Verify(b => b.DeleteAllPositionsAsync(It.IsAny<int>()), Times.Never);
        _matcher.Verify(m => m.PreFetchArticlesAsync("FS27", It.Is<IEnumerable<string>>(n => n.SequenceEqual(ArticleNumbers))), Times.Once);
        _matcher.Verify(m => m.PreFetchArticlesAsync("NOOS", It.IsAny<IEnumerable<string>>()), Times.Once);
        _matcher.Verify(m => m.FindArticleAsync(It.Is<SgdArticleCriteria>(c => c.Color == "1001 Black" && c.SeasonCode == "FS27" && c.FallbackSeasonCodes!.SequenceEqual(FallbackCodes))), Times.Once);
        _positionsDeleted.Should().BeFalse();
        _warning.Should().BeNull();
    }

    [Test]
    public async Task ExecuteAsync_WhenGrouped_ShouldUseTheGroupedTemplateWithTheSizeRows()
    {
        SetupNewOrder();
        _settings.PositionGroupingMode = SgdPositionGroupingMode.GroupedSizePosition;
        _settings.GroupedPositionTextTemplate = "{ArticleNumber} {ArticleName}: {SizesRows} - {BexioArticleDescription}";
        var order = Order();
        order.Positions[0].Size = "1x Size S<br />1x Size M";

        await Importer().ExecuteAsync(order, _settings, Interaction());

        _bexio.Verify(b => b.AddArticlePositionAsync(77, 1, It.IsAny<OrderPosition>(), "E1 Jacket: 1x Size S<br />1x Size M - Description 1"), Times.Once);
        _bexio.Verify(b => b.AddDiscountPositionAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenAnArticleHasNoPrice_ShouldWarnAndUseZero()
    {
        SetupNewOrder();
        SetupArticles(price: null);
        var order = Order();

        await Importer().ExecuteAsync(order, _settings, Interaction());

        order.Positions[0].GrossUnitPrice.Should().Be(0m);
        _logs.Should().Contain("⚠️ Article 'FS27E1' has no sale price in Bexio.");
    }

    [Test]
    public async Task ExecuteAsync_WhenArticlesAreMissing_ShouldListThemAllAndChangeNothingInBexio()
    {
        SetupExistingOrder();
        _matcher.Setup(m => m.FindArticleAsync(It.IsAny<SgdArticleCriteria>())).ReturnsAsync(ArticleMatch.NotFound());

        Func<Task> act = () => Importer().ExecuteAsync(Order(), _settings, Interaction());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("2 article(s) not found*'FS27'*'E1' with color '1001 Black' (row 2), 'E2' with color 'Red' (row 3)*Nothing was changed*");
        _calls.Should().BeEmpty();
        _positionsDeleted.Should().BeFalse();
        _bexio.Verify(b => b.FindOrderByDocumentNrAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithAnExistingOrder_ShouldDeleteItsPositionsBeforeAddingTheNewOnes()
    {
        SetupExistingOrder();
        _texts.Clear();
        _texts.Enqueue("1001");
        _texts.Enqueue(" ");
        _texts.Enqueue("AU-12");
        var order = Order();

        var result = await Importer().ExecuteAsync(order, _settings, Interaction());

        result.Should().Be(new ImportResult(Success: true, OrderNumber: "AU-12", UploadedPositionsCount: 2));
        _askedTexts.Skip(1).Should().Equal(
            (SgdTranslations.Import_OrderNumberTitle, null),
            (SgdTranslations.Import_OrderNumberTitle, SgdTranslations.Import_OrderNumberRequired));
        _warning.Should().Be(string.Format(SgdTranslations.Import_ReplaceWarningMessage, "AU-12"));
        _calls.Should().Equal("delete", "add", "add");
        _positionsDeleted.Should().BeTrue();
        order.OrderNumber.Should().Be("AU-12");
        _bexio.Verify(b => b.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
        _logs.Should().Contain("4 positions deleted.");
    }

    [Test]
    public async Task ExecuteAsync_WhenTheWarningIsDeclined_ShouldCancelWithoutTouchingTheOrder()
    {
        SetupExistingOrder();

        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction(acceptWarning: false));

        result.ErrorMessage.Should().Be("Cancelled by user.");
        _calls.Should().BeEmpty();
        _bexio.Verify(b => b.FindOrderByDocumentNrAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenTheOrderNumberDialogIsCancelled_ShouldCancelWithoutWarning()
    {
        SetupExistingOrder();
        _texts.Clear();
        _texts.Enqueue("1001");

        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction());

        result.ErrorMessage.Should().Be("Cancelled by user.");
        _warning.Should().BeNull();
        _calls.Should().BeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_WhenTheExistingOrderBelongsToAnotherCustomer_ShouldFailWithoutDeleting()
    {
        SetupExistingOrder(contactNr: "2002");

        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("Customer number mismatch");
        _calls.Should().BeEmpty();
        _positionsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task ExecuteAsync_WhenTheExistingOrderIsNotFound_ShouldFailWithoutDeleting()
    {
        SetupExistingOrder();
        _bexio.Setup(b => b.FindOrderByDocumentNrAsync("AU-12")).ReturnsAsync((BexioOrder?)null);

        var result = await Importer().ExecuteAsync(Order(), _settings, Interaction());

        result.ErrorMessage.Should().Be("Order AU-12 not found.");
        _calls.Should().BeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_WhenDeletingFails_ShouldReportTheDeletionFirst()
    {
        SetupExistingOrder();
        _bexio.Setup(b => b.DeleteAllPositionsAsync(12)).ThrowsAsync(new HttpRequestException("locked"));

        Func<Task> act = () => Importer().ExecuteAsync(Order(), _settings, Interaction());

        await act.Should().ThrowAsync<HttpRequestException>();
        _positionsDeleted.Should().BeTrue("some positions may already be deleted");
    }

    [Test]
    public async Task ExecuteAsync_WhenArgumentsAreNull_ShouldThrow()
    {
        await ((Func<Task>)(() => Importer().ExecuteAsync(null!, _settings, Interaction()))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => Importer().ExecuteAsync(Order(), null!, Interaction()))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => Importer().ExecuteAsync(Order(), _settings, null!))).Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void Constructor_ShouldRequireBexioAndCreateTheDefaultMatcher()
    {
        ((Action)(() => _ = new SgdImporter(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new SgdImporter(_bexio.Object))).Should().NotThrow();
    }
}
