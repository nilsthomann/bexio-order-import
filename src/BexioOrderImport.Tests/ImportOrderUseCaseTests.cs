using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Models;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

[NotInParallel]
public class ImportOrderUseCaseTests
{
    private readonly Mock<IExcelParser> _parserMock;
    private readonly Mock<IBexioClient> _clientMock;
    private readonly ImportOrderUseCase _useCase;

    public ImportOrderUseCaseTests()
    {
        _parserMock = new Mock<IExcelParser>();
        _clientMock = new Mock<IBexioClient>();
        _useCase = new ImportOrderUseCase(_parserMock.Object, _clientMock.Object);
    }

    [Test]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactExists_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>())).ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description Text", Name = "Product Name Text" });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderNumber.Should().Be("AU-00456");
        _clientMock.Verify(c => c.CreateContactAsync(It.IsAny<Customer>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(123, order), Times.Once);
        _clientMock.Verify(c => c.AddArticlePositionAsync(456, 789, order.Positions[0], It.IsAny<string?>()), Times.Once);

        loggedMessages.Should().Contain(m => m.Contains("Order created successfully"));
        loggedMessages.Should().Contain(m => m.Contains("Successfully completed"));
    }

    [Test]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactDoesNotExistAndUserConfirmsContactCreation_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync((int?)null);
        _clientMock.Setup(c => c.CreateContactAsync(order.Customer)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>())).ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description Text", Name = "Product Name Text" });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        _clientMock.Verify(c => c.CreateContactAsync(order.Customer), Times.Once);
        _clientMock.Verify(c => c.CreateOrderAsync(123, order), Times.Once);
        _clientMock.Verify(c => c.AddArticlePositionAsync(456, 789, order.Positions[0], It.IsAny<string?>()), Times.Once);

        loggedMessages.Should().Contain(m => m.Contains("Creating new customer in Bexio"));
        loggedMessages.Should().Contain(m => m.Contains("Successfully completed"));
    }

    [Test]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactDoesNotExistAndUserRejectsContactCreation_ShouldAbort()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync((int?)null);

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(false),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        _clientMock.Verify(c => c.CreateContactAsync(It.IsAny<Customer>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);

        loggedMessages.Should().Contain(m => m.Contains("Order import cancelled (customer was not created)."));
    }

    [Test]
    public async Task ExecuteAsync_WhenUploadRejected_ShouldAbortAndLogCorrectMessage()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(false),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        _clientMock.Verify(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
        loggedMessages.Should().Contain("Order import cancelled.");
    }

    [Test]
    public async Task ExecuteAsync_WithNoPositions_ReturnsFalse()
    {
        var emptyOrder = new Order { Positions = [] };
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(emptyOrder);

        var interaction = new DelegateImportUserInteractionService();
        var result = await _useCase.ExecuteAsync("test.xlsx", interaction);
        result.Success.Should().BeFalse();
    }

    [Test]
    public async Task ExecuteAsync_WhenArticleNotFound_ThrowsException()
    {
        // Arrange
        var order = new Order
        {
            Customer = new Customer { Email = "t@t.com", CompanyName = "Test AG" },
            Positions =
            [
                new OrderPosition { ArticleNumber = "UNKNOWN", ArticleName = "Part", Quantity = 1, UnitPrice = 10m }
            ]
        };
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((BexioArticle?)null);

        var interaction = new DelegateImportUserInteractionService();

        // Act
        Func<Task> act = () => _useCase.ExecuteAsync("test.xlsx", interaction);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*UNKNOWN*");
        _clientMock.Verify(c => c.AddArticlePositionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenNoOrderNumberOrCustomerNumberAndEmailMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var order = new Order
        {
            Customer = new Customer { Email = string.Empty, CompanyName = "Test AG" },
            Positions =
            [
                new OrderPosition { ArticleNumber = "ART1", ArticleName = "Part", Quantity = 1, UnitPrice = 10m }
            ]
        };
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);

        var interaction = new DelegateImportUserInteractionService(
            confirmUpload: () => Task.FromResult(true)
        );

        // Act
        Func<Task> act = () => _useCase.ExecuteAsync("test.xlsx", interaction);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Email address is required*");
        _clientMock.Verify(c => c.FindContactIdAsync(It.IsAny<string>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumberAndEmailMatch_ShouldImportDirectlyToExistingOrder()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = "AU-00456";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindOrderByDocumentNrAsync("AU-00456")).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456", ContactId = 123 });
        _clientMock.Setup(c => c.GetContactDetailsAsync(123)).ReturnsAsync(new BexioContact { Id = 123, Nr = "10001", EMail = order.Customer.Email });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>())).ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description Text", Name = "Product Name Text" });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderNumber.Should().Be("AU-00456");
        _clientMock.Verify(c => c.FindContactIdAsync(It.IsAny<string>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
        _clientMock.Verify(c => c.AddArticlePositionAsync(456, 789, order.Positions[0], It.IsAny<string?>()), Times.Once);
        loggedMessages.Should().Contain(m => m.Contains("Existing order matched"));
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumberAndMatchingCustomerNumber_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = "AU-00456";
        order.CustomerNumber = "10099";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindOrderByDocumentNrAsync("AU-00456")).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456", ContactId = 99 });
        _clientMock.Setup(c => c.GetContactDetailsAsync(99)).ReturnsAsync(new BexioContact { Id = 99, Nr = "10099", EMail = "different@domain.com" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>())).ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description Text", Name = "Product Name Text" });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(false),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderNumber.Should().Be("AU-00456");
        loggedMessages.Should().Contain(m => m.Contains("Customer number matched (10099)"));
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumberAndMismatchedCustomerNumber_ShouldFailWithCustomerNumberMismatchError()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = "AU-00456";
        order.CustomerNumber = "10099";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindOrderByDocumentNrAsync("AU-00456")).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456", ContactId = 100 });
        _clientMock.Setup(c => c.GetContactDetailsAsync(100)).ReturnsAsync(new BexioContact { Id = 100, Nr = "10100", EMail = order.Customer.Email });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Customer number mismatch");
        loggedMessages.Should().Contain(m => m.Contains("Customer number mismatch"));
    }

    [Test]
    public async Task ExecuteAsync_WithCustomerNumberOnly_ShouldCreateOrderForSpecificCustomerNumber()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = null;
        order.CustomerNumber = "10099";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactByNrAsync("10099")).ReturnsAsync(new BexioContact { Id = 99, Nr = "10099", Name = "Test Customer", EMail = "test@domain.com" });
        _clientMock.Setup(c => c.CreateOrderAsync(99, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>())).ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description Text", Name = "Product Name Text" });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderNumber.Should().Be("AU-00456");
        _clientMock.Verify(c => c.FindContactIdAsync(It.IsAny<string>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(99, order), Times.Once);
        loggedMessages.Should().Contain(m => m.Contains("Customer number provided (10099)"));
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumberAndEmailMismatch_WhenUserConfirms_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = "AU-00456";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindOrderByDocumentNrAsync("AU-00456")).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456", ContactId = 123 });
        _clientMock.Setup(c => c.GetContactDetailsAsync(123)).ReturnsAsync(new BexioContact() { Id = 123, Nr = "10001", EMail = "different@domain.com" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>())).ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description Text", Name = "Product Name Text" });

        var loggedMessages = new List<string>();
        bool mismatchCallbackCalled = false;
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) =>
            {
                mismatchCallbackCalled = true;
                return Task.FromResult(true);
            },
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeTrue();
        mismatchCallbackCalled.Should().BeTrue();
        _clientMock.Verify(c => c.AddArticlePositionAsync(456, 789, order.Positions[0], It.IsAny<string?>()), Times.Once);
        loggedMessages.Should().Contain(m => m.Contains("Email mismatch ignored by user"));
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumberAndEmailMismatch_WhenUserRejects_ShouldAbort()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = "AU-00456";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindOrderByDocumentNrAsync("AU-00456")).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456", ContactId = 123 });
        _clientMock.Setup(c => c.GetContactDetailsAsync(123)).ReturnsAsync(new BexioContact() { Id = 123, Nr = "10001", EMail = "different@domain.com" });

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(false),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeFalse();
        _clientMock.Verify(c => c.AddArticlePositionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Never);
        loggedMessages.Should().Contain(m => m.Contains("Email mismatch"));
    }

    [Test]
    public async Task ExecuteAsync_WithCustomerNumber_WhenCustomerNotFound_ShouldAbort()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = null;
        order.CustomerNumber = "888";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactByNrAsync("888")).ReturnsAsync((BexioContact?)null);

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Customer 888 not found");
        loggedMessages.Should().Contain(m => m.Contains("Customer #888 not found in Bexio"));
    }

    [Test]
    public async Task ExecuteAsync_WithOrderNumber_WhenOrderNotFound_ShouldAbort()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.OrderNumber = "AU-00999";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindOrderByDocumentNrAsync("AU-00999")).ReturnsAsync((BexioOrder?)null);

        var loggedMessages = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            showPreview: o => { },
            confirmUpload: () => Task.FromResult(true),
            confirmCustomerCreation: c => Task.FromResult(true),
            confirmEmailMismatch: (ex, el) => Task.FromResult(true),
            logInfo: loggedMessages.Add
        );

        // Act
        var result = await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        result.Success.Should().BeFalse();
        _clientMock.Verify(c => c.AddArticlePositionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderPosition>(), It.IsAny<string?>()), Times.Never);
        loggedMessages.Should().Contain(m => m.Contains("Order #AU-00999 not found"));
    }

    [Test]
    public async Task ExecuteAsync_ShouldQueryArticleWithSeasonCodeAndColor()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.Positions[0].Color = "Black";
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });

        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", "FS27"))
            .ReturnsAsync(new BexioArticle { Id = 789, Description = "Product Description", Name = "Product Name" });

        var interaction = new DelegateImportUserInteractionService();
        var options = new ImportOrderOptions(SeasonCode: "FS27");

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction, options);

        // Assert
        _clientMock.Verify(c => c.FindArticleAsync("123", "Black", "FS27"), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenOrderHasGlobalDiscount_ShouldCallAddDiscountPositionAsync()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.DiscountPercent = 10m; // 10% global discount
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>()))
            .ReturnsAsync(new BexioArticle { Id = 789, Description = "Desc", Name = "Name" });

        var interaction = new DelegateImportUserInteractionService();

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        _clientMock.Verify(c => c.AddDiscountPositionAsync(456, 10m, "Rabatt (10%)"), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithCustomDiscountPositionTextTemplate_ShouldFormatTextCorrectly()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.DiscountPercent = 15m;
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>()))
            .ReturnsAsync(new BexioArticle { Id = 789, Description = "Desc", Name = "Name" });

        var interaction = new DelegateImportUserInteractionService();
        var options = new ImportOrderOptions(DiscountPositionTextTemplate: "Sonderrabatt ({DiscountInPercent}%)");

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction, options);

        // Assert
        _clientMock.Verify(c => c.AddDiscountPositionAsync(456, 15m, "Sonderrabatt (15%)"), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenOrderHasNoGlobalDiscount_ShouldNotCallAddDiscountPositionAsync()
    {
        // Arrange
        var order = CreateSampleOrder();
        order.DiscountPercent = 0m;
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", It.IsAny<string>()))
            .ReturnsAsync(new BexioArticle { Id = 789, Description = "Desc", Name = "Name" });

        var interaction = new DelegateImportUserInteractionService();

        // Act
        await _useCase.ExecuteAsync("dummy.xlsx", interaction);

        // Assert
        _clientMock.Verify(c => c.AddDiscountPositionAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WithInteractionInterfaceAndOptions_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(new BexioOrder { Id = 456, DocumentNr = "AU-00456" });
        _clientMock.Setup(c => c.FindArticleAsync("123", "Black", "SS26")).ReturnsAsync(new BexioArticle { Id = 789, Description = "Desc", Name = "Name" });

        var interactionMock = new Mock<IImportUserInteractionService>();
        interactionMock.Setup(i => i.ConfirmUploadAsync()).ReturnsAsync(true);
        interactionMock.Setup(i => i.ConfirmCustomerCreationAsync(It.IsAny<Customer>())).ReturnsAsync(true);
        interactionMock.Setup(i => i.ConfirmEmailMismatchAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var options = new ImportOrderOptions(SeasonCode: "SS26");

        // Act
        var result = await _useCase.ExecuteAsync("file.xlsx", interactionMock.Object, options);

        // Assert
        result.Success.Should().BeTrue();
        result.OrderNumber.Should().Be("AU-00456");
        _clientMock.Verify(c => c.FindArticleAsync("123", "Black", "SS26"), Times.Once);
        interactionMock.Verify(i => i.LogInfo(It.Is<string>(s => s.Contains("Successfully completed"))), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WhenExcelParserIsNull_ShouldThrowInvalidOperationException()
    {
        var useCase = new ImportOrderUseCase(_clientMock.Object);
        var interaction = new DelegateImportUserInteractionService();

        Func<Task> act = async () => await useCase.ExecuteAsync("file.xlsx", interaction);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*IExcelParser is required*");
    }

    [Test]
    public async Task ExecuteAsync_WhenCustomerCreationRefusedByUser_ShouldReturnCancelledResult()
    {
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync((int?)null);

        var interactionMock = new Mock<IImportUserInteractionService>();
        interactionMock.Setup(i => i.ConfirmUploadAsync()).ReturnsAsync(true);
        interactionMock.Setup(i => i.ConfirmCustomerCreationAsync(It.IsAny<Customer>())).ReturnsAsync(false);

        var result = await _useCase.ExecuteAsync("file.xlsx", interactionMock.Object);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("customer creation refused");
        _clientMock.Verify(c => c.CreateContactAsync(It.IsAny<Customer>()), Times.Never);
    }

    private static Order CreateSampleOrder()
    {
        var order = new Order
        {
            Customer = new Customer
            {
                CompanyName = "Test Firma",
                Email = "test@domain.com"
            }
        };
        order.Positions.Add(new OrderPosition
        {
            ArticleNumber = "123",
            ArticleName = "Test Artikel",
            Color = "Black",
            Quantity = 2,
            UnitPrice = 10.0m
        });
        return order;
    }
}
