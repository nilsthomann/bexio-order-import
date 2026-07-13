using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

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

    [Fact]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactExists_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(456);
        _clientMock.Setup(c => c.FindArticleIdAsync("123")).ReturnsAsync(789);

        var loggedMessages = new List<string>();

        // Act
        await _useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: o => { },
            confirmUploadCallback: () => Task.FromResult(true),
            confirmCustomerCreationCallback: c => Task.FromResult(true),
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        _clientMock.Verify(c => c.CreateContactAsync(It.IsAny<Customer>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(123, order), Times.Once);
        _clientMock.Verify(c => c.AddArticlePositionAsync(456, 789, order.Positions[0]), Times.Once);

        loggedMessages.Should().Contain(m => m.Contains("Order created successfully"));
        loggedMessages.Should().Contain(m => m.Contains("Successfully completed"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactDoesNotExistAndUserConfirmsContactCreation_ShouldImportSuccessfully()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync((int?)null);
        _clientMock.Setup(c => c.CreateContactAsync(order.Customer)).ReturnsAsync(123);
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(456);
        _clientMock.Setup(c => c.FindArticleIdAsync("123")).ReturnsAsync(789);

        var loggedMessages = new List<string>();

        // Act
        await _useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: o => { },
            confirmUploadCallback: () => Task.FromResult(true),
            confirmCustomerCreationCallback: c => Task.FromResult(true),
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        _clientMock.Verify(c => c.CreateContactAsync(order.Customer), Times.Once);
        _clientMock.Verify(c => c.CreateOrderAsync(123, order), Times.Once);
        _clientMock.Verify(c => c.AddArticlePositionAsync(456, 789, order.Positions[0]), Times.Once);

        loggedMessages.Should().Contain(m => m.Contains("Creating new customer in Bexio"));
        loggedMessages.Should().Contain(m => m.Contains("Successfully completed"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactDoesNotExistAndUserRejectsContactCreation_ShouldAbort()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);
        _clientMock.Setup(c => c.FindContactIdAsync(order.Customer.Email)).ReturnsAsync((int?)null);

        var loggedMessages = new List<string>();

        // Act
        await _useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: o => { },
            confirmUploadCallback: () => Task.FromResult(true),
            confirmCustomerCreationCallback: c => Task.FromResult(false),
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        _clientMock.Verify(c => c.CreateContactAsync(It.IsAny<Customer>()), Times.Never);
        _clientMock.Verify(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);

        loggedMessages.Should().Contain(m => m.Contains("Order import cancelled (customer was not created)."));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadRejected_ShouldAbortAndLogCorrectMessage()
    {
        // Arrange
        var order = CreateSampleOrder();
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(order);

        var loggedMessages = new List<string>();

        // Act
        await _useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: o => { },
            confirmUploadCallback: () => Task.FromResult(false),
            confirmCustomerCreationCallback: c => Task.FromResult(true),
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        _clientMock.Verify(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>()), Times.Never);
        loggedMessages.Should().Contain("Order import cancelled.");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoPositions_ReturnsFalse()
    {
        var emptyOrder = new Order { Positions = [] };
        _parserMock.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(emptyOrder);

        bool result = await _useCase.ExecuteAsync(
            "test.xlsx", _ => { }, () => Task.FromResult(true), _ => Task.FromResult(true), _ => { }
        );
        result.Should().BeFalse();
    }

    [Fact]
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
        _clientMock.Setup(c => c.CreateOrderAsync(123, order)).ReturnsAsync(456);
        _clientMock.Setup(c => c.FindArticleIdAsync("UNKNOWN")).ReturnsAsync((int?)null);

        // Act
        Func<Task> act = () => _useCase.ExecuteAsync(
            "test.xlsx", _ => { }, () => Task.FromResult(true), _ => Task.FromResult(true), _ => { }
        );

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*UNKNOWN*");
        _clientMock.Verify(c => c.AddArticlePositionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<OrderPosition>()), Times.Never);
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
            Quantity = 2,
            UnitPrice = 10.0m
        });
        return order;
    }
}
