using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Tests.Utils;
using FluentAssertions;
using Moq;
using System.Net;

namespace BexioOrderImport.Tests;

public class BexioApiClientTests
{
    [Fact]
    public async Task FindContactIdAsync_WhenContactExists_ShouldReturnContactId()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 12345, \"name_1\": \"Test Company\"}]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindContactIdAsync("test@company.com");

        // Assert
        result.Should().Be(12345);
    }

    [Fact]
    public async Task FindContactIdAsync_WhenContactDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindContactIdAsync("none@company.com");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateContactAsync_ShouldReturnNewContactId()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\": 98765}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        var customer = new Customer
        {
            CompanyName = "New Partner",
            Email = "partner@domain.com"
        };

        // Act
        var result = await client.CreateContactAsync(customer);

        // Assert
        result.Should().Be(98765);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidOrder_ReturnsOrderId()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\": 11111}", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        var order = new Order { Customer = new Customer { CompanyName = "Test AG" } };

        // Act
        var result = await client.CreateOrderAsync(12345, order);

        // Assert
        result.Should().Be(11111);
    }

    [Fact]
    public async Task FindArticleAsync_WithKnownArticle_ReturnsArticle()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 77777, \"text\": \"Sample Product Description\"}]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindArticleAsync("ART-001"); ;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(77777);
        result.Text.Should().Be("Sample Product Description");
    }

    [Fact]
    public async Task FindArticleAsync_WithDuplicateArticles_ThrowsDuplicateArticleException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 11}, {\"id\": 22}]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        Func<Task> act = () => client.FindArticleAsync("DUPLICATE");

        // Assert
        await act.Should().ThrowAsync<DuplicateArticleException>()
            .Where(e => e.SearchQuery == "DUPLICATE" && e.MatchCount == 2);
    }

    [Fact]
    public async Task FindArticleAsync_WithUnknownArticle_ReturnsNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindArticleAsync("UNKNOWN");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AddArticlePositionAsync_Succeeds()
    {
        // Arrange
        bool requestSent = false;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var uri = req.RequestUri!.ToString();
                if (uri.Contains("accounts"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[{\"id\": 3200, \"account_no\": \"3200\", \"is_active\": true}]", System.Text.Encoding.UTF8, "application/json")
                    });
                }
                if (uri.Contains("3.0/taxes"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[{\"id\": 1, \"code\": \"1\", \"is_active\": true}]", System.Text.Encoding.UTF8, "application/json")
                    });
                }
                if (uri.Contains("kb_order/123/kb_position_article"))
                {
                    requestSent = true;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var pos = new OrderPosition { Quantity = 5, Color = "Red", Size = "M", UnitPrice = 12.5m, DiscountPercent = 10m };

        // Act
        await client.AddArticlePositionAsync(123, 77777, pos);

        // Assert
        requestSent.Should().BeTrue();
    }



    [Fact]
    public async Task FindContactIdAsync_WhenApiReturns500_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Internal Server Error"))
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindContactIdAsync("error@error.com"));
    }


    [Fact]
    public void BexioClientFactory_Create_ShouldReturnBexioApiClient()
    {
        // Arrange
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient());
        var factory = new BexioClientFactory(mockFactory.Object);

        // Act
        var client = factory.Create("my-token", 1, 1);

        // Assert
        client.Should().NotBeNull();
        client.Should().BeOfType<BexioApiClient>();
    }

    [Fact]
    public async Task CreateContactAsync_WhenApiReturnsNullContact_ThrowsException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var customer = new Customer { CompanyName = "New Partner", Email = "partner@domain.com" };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.CreateContactAsync(customer));
    }

    [Fact]
    public async Task CreateOrderAsync_WhenApiReturnsNullOrder_ThrowsException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var order = new Order { Customer = new Customer { CompanyName = "Test AG" } };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.CreateOrderAsync(12345, order));
    }

    [Fact]
    public async Task GetAccountsAsync_ReturnsAccountsList()
    {
        // Arrange
        var jsonResponse = @"[
            { ""id"": 1, ""name"": ""Cash"", ""account_no"": ""1000"", ""is_active"": true, ""account_type"": 1 },
            { ""id"": 2, ""name"": ""Sales"", ""account_no"": ""3200"", ""is_active"": true, ""account_type"": 1 },
            { ""id"": 3, ""name"": ""Inactive"", ""account_no"": ""4000"", ""is_active"": false, ""account_type"": 1 },
            { ""id"": 4, ""name"": ""Type2"", ""account_no"": ""5000"", ""is_active"": true, ""account_type"": 2 }
        ]";
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                req.RequestUri!.ToString().Should().Contain("accounts");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.GetAccountsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("Cash");
        result[0].AccountNo.Should().Be("1000");
        result[0].IsActive.Should().BeTrue();
        result[0].AccountType.Should().Be(1);
    }

    [Fact]
    public async Task GetTaxesAsync_ReturnsTaxesList()
    {
        // Arrange
        var jsonResponse = @"[
            { ""id"": 10, ""name"": ""MwSt 7.7"", ""percentage"": 7.7, ""is_active"": true, ""code"": ""MWST_77"", ""display_name"": ""MwSt 7.7"", ""type"": ""sales_tax"" },
            { ""id"": 11, ""name"": ""MwSt 8.1"", ""percentage"": 8.1, ""is_active"": true, ""code"": ""MWST_81"", ""display_name"": ""MwSt 8.1"", ""type"": ""sales_tax"" },
            { ""id"": 12, ""name"": ""Inactive"", ""percentage"": 8.1, ""is_active"": false, ""code"": ""INACTIVE"", ""display_name"": ""Inactive"", ""type"": ""sales_tax"" },
            { ""id"": 13, ""name"": ""UEX"", ""percentage"": 8.1, ""is_active"": true, ""code"": ""UEX"", ""display_name"": ""UEX 0"", ""type"": ""not_taxable_turnover"" },
            { ""id"": 14, ""name"": ""OtherType"", ""percentage"": 8.1, ""is_active"": true, ""code"": ""OTHER"", ""display_name"": ""Other"", ""type"": ""other_tax"" }
        ]";
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                req.RequestUri!.ToString().Should().Contain("3.0/taxes");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.GetTaxesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
        result[0].Id.Should().Be(10);
        result[0].Percentage.Should().Be(7.7m);
        result[0].IsActive.Should().BeTrue();
        result[0].Code.Should().Be("MWST_77");
        result[0].DisplayName.Should().Be("MwSt 7.7");
        result[0].Type.Should().Be("sales_tax");
    }
}
