using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Tests;

public class ImportOrderUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactExists_ShouldImportSuccessfully()
    {
        // Arrange
        var parser = new MockExcelParser();
        var client = new MockBexioClient { ContactIdToReturn = 123 };
        var useCase = new ImportOrderUseCase(parser, client);

        var loggedMessages = new List<string>();
        parser.OrderToReturn = CreateSampleOrder();

        // Act
        await useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: order => { },
            confirmUploadCallback: () => Task.FromResult(true),
            confirmCustomerCreationCallback: customer => Task.FromResult(true),
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        client.CreateContactCalled.Should().BeFalse();
        client.CreateOrderCalled.Should().BeTrue();
        client.AddArticlePositionCount.Should().Be(1);
        client.AddCustomPositionCount.Should().Be(0);
        
        loggedMessages.Should().Contain(m => m.Contains("Order created successfully"));
        loggedMessages.Should().Contain(m => m.Contains("Successfully completed"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactDoesNotExistAndUserConfirmsContactCreation_ShouldImportSuccessfully()
    {
        // Arrange
        var parser = new MockExcelParser();
        var client = new MockBexioClient { ContactIdToReturn = null }; // Contact not found
        var useCase = new ImportOrderUseCase(parser, client);

        var loggedMessages = new List<string>();
        parser.OrderToReturn = CreateSampleOrder();

        // Act
        await useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: order => { },
            confirmUploadCallback: () => Task.FromResult(true),
            confirmCustomerCreationCallback: customer => Task.FromResult(true), // User confirms creation
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        client.CreateContactCalled.Should().BeTrue();
        client.CreateOrderCalled.Should().BeTrue();
        client.AddArticlePositionCount.Should().Be(1);
        
        loggedMessages.Should().Contain(m => m.Contains("Creating new customer in Bexio"));
        loggedMessages.Should().Contain(m => m.Contains("Successfully completed"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadConfirmedAndContactDoesNotExistAndUserRejectsContactCreation_ShouldAbort()
    {
        // Arrange
        var parser = new MockExcelParser();
        var client = new MockBexioClient { ContactIdToReturn = null };
        var useCase = new ImportOrderUseCase(parser, client);

        var loggedMessages = new List<string>();
        parser.OrderToReturn = CreateSampleOrder();

        // Act
        await useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: order => { },
            confirmUploadCallback: () => Task.FromResult(true),
            confirmCustomerCreationCallback: customer => Task.FromResult(false), // User rejects creation
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        client.CreateContactCalled.Should().BeFalse();
        client.CreateOrderCalled.Should().BeFalse();
        
        loggedMessages.Should().Contain(m => m.Contains("Order import cancelled (customer was not created)."));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUploadRejected_ShouldAbortAndLogCorrectMessage()
    {
        // Arrange
        var parser = new MockExcelParser();
        var client = new MockBexioClient();
        var useCase = new ImportOrderUseCase(parser, client);

        var loggedMessages = new List<string>();
        parser.OrderToReturn = CreateSampleOrder();

        // Act
        await useCase.ExecuteAsync(
            "dummy.xlsx",
            showPreviewCallback: order => { },
            confirmUploadCallback: () => Task.FromResult(false), // User rejects upload
            confirmCustomerCreationCallback: customer => Task.FromResult(true),
            logInfoCallback: loggedMessages.Add
        );

        // Assert
        client.CreateOrderCalled.Should().BeFalse();
        loggedMessages.Should().Contain("Order import cancelled.");
    }

    private Order CreateSampleOrder()
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

    // Manual mock classes for Clean Architecture testing
    private class MockExcelParser : IExcelParser
    {
        public Order OrderToReturn { get; set; } = new();
        public Order ParseOrderForm(string filePath) => OrderToReturn;
    }

    private class MockBexioClient : IBexioClient
    {
        public int? ContactIdToReturn { get; set; } = 123;
        public int OrderIdToReturn { get; set; } = 456;
        public int? ArticleIdToReturn { get; set; } = 789;

        public bool CreateContactCalled { get; private set; }
        public bool CreateOrderCalled { get; private set; }
        public int AddArticlePositionCount { get; private set; }
        public int AddCustomPositionCount { get; private set; }

        public Task<int?> FindContactIdAsync(string email) => Task.FromResult(ContactIdToReturn);
        
        public Task<int> CreateContactAsync(Customer customer)
        {
            CreateContactCalled = true;
            return Task.FromResult(1234);
        }
        
        public Task<int> CreateOrderAsync(int contactId, Order order)
        {
            CreateOrderCalled = true;
            return Task.FromResult(OrderIdToReturn);
        }
        
        public Task<int?> FindArticleIdAsync(string articleNumber, string articleName) => Task.FromResult(ArticleIdToReturn);
        
        public Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position)
        {
            AddArticlePositionCount++;
            return Task.CompletedTask;
        }
        
        public Task AddCustomPositionAsync(int orderId, OrderPosition position)
        {
            AddCustomPositionCount++;
            return Task.CompletedTask;
        }
    }
}
