using BexioOrderImport.Domain.Models;
using BexioOrderImport.Infrastructure.Bexio;
using FluentAssertions;
using System.Net;

namespace BexioOrderImport.Tests;

public class BexioApiClientTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncFunc { get; set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsyncFunc(request, cancellationToken);
        }
    }

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
        var client = new BexioApiClient(httpClient, "dummy-token");

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
        var client = new BexioApiClient(httpClient, "dummy-token");

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
        var client = new BexioApiClient(httpClient, "dummy-token");

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
        var client = new BexioApiClient(httpClient, "dummy-token");

        var order = new Order { Customer = new Customer { CompanyName = "Test AG" } };

        // Act
        var result = await client.CreateOrderAsync(12345, order);

        // Assert
        result.Should().Be(11111);
    }

    [Fact]
    public async Task FindArticleIdAsync_WithKnownArticle_ReturnsId()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 77777}]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token");

        // Act
        var result = await client.FindArticleIdAsync("ART-001", "Some Article");

        // Assert
        result.Should().Be(77777);
    }

    [Fact]
    public async Task FindArticleIdAsync_WithUnknownArticle_ReturnsNull()
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
        var client = new BexioApiClient(httpClient, "dummy-token");

        // Act
        var result = await client.FindArticleIdAsync("UNKNOWN", "Unknown Article");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindArticleIdAsync_WhenCodeNotFoundButNameFound_ReturnsId()
    {
        // Arrange
        int callCount = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                callCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (callCount == 1)
                {
                    // First search (by code) -> empty list
                    response.Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json");
                }
                else
                {
                    // Second search (by name) -> returns matching article
                    response.Content = new StringContent("[{\"id\": 88888}]", System.Text.Encoding.UTF8, "application/json");
                }
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token");

        // Act
        var result = await client.FindArticleIdAsync("ART-CODE-MISSED", "My Article Title");

        // Assert
        result.Should().Be(88888);
        callCount.Should().Be(2);
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
                req.RequestUri!.ToString().Should().Contain("kb_order/123/kb_position_article");
                requestSent = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token");
        var pos = new OrderPosition { Quantity = 5, Color = "Red", Size = "M", UnitPrice = 12.5m, DiscountPercent = 10m };

        // Act
        await client.AddArticlePositionAsync(123, 77777, pos);

        // Assert
        requestSent.Should().BeTrue();
    }

    [Fact]
    public async Task AddCustomPositionAsync_Succeeds()
    {
        // Arrange
        bool requestSent = false;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                req.RequestUri!.ToString().Should().Contain("kb_order/123/kb_position_custom");
                requestSent = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 3200, 1);
        var pos = new OrderPosition { Quantity = 3, ArticleNumber = "CUSTOM", ArticleName = "Special Item", Size = "L", Color = "Blue", UnitPrice = 15m, DiscountPercent = 0m };

        // Act
        await client.AddCustomPositionAsync(123, pos);

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
        var client = new BexioApiClient(httpClient, "dummy-token");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindContactIdAsync("error@error.com"));
    }

    private class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    [Fact]
    public void BexioClientFactory_Create_ShouldReturnBexioApiClient()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory();
        var factory = new BexioClientFactory(mockFactory);

        // Act
        var client = factory.Create("my-token", 3200, 1);

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
        var client = new BexioApiClient(httpClient, "dummy-token");
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
        var client = new BexioApiClient(httpClient, "dummy-token");
        var order = new Order { Customer = new Customer { CompanyName = "Test AG" } };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => client.CreateOrderAsync(12345, order));
    }
}
