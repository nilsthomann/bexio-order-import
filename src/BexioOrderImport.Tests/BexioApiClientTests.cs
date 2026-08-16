using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Tests.Utils;
using FluentAssertions;
using Moq;
using System.Net;
using System.Text;

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
                    Content = new StringContent("[{\"id\": 77777, \"intern_name\": \"Sample Product Name\",\"intern_description\": \"Sample Product Description\"}]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindArticleAsync("ART-001", "Black", "FS27"); ;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(77777);
        result.Name.Should().Be("Sample Product Name");
        result.Description.Should().Be("Sample Product Description");
    }

    [Fact]
    public async Task FindArticleAsync_WithDuplicateArticles_Fallback_To_Filter_ReturnsArticle()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[" +
                    "{\"id\": 77777, \"intern_name\": \"FS27 Sample Product Name Black\",\"intern_description\": \"Sample Product Description\"}," +
                    "{\"id\": 77778, \"intern_name\": \"FS27 Sample Product Name White\",\"intern_description\": \"Sample Product Description\"}]"
                    , System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindArticleAsync("ART-001", "Black", "FS27"); ;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(77777);
        result.Name.Should().Be("FS27 Sample Product Name Black");
        result.Description.Should().Be("Sample Product Description");
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
        var result = await client.FindArticleAsync("ART-01","Black", "FS27");

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
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreateContactAsync(customer));
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
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreateOrderAsync(12345, order));
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

    [Fact]
    public async Task GetOrderContactEmailAsync_WhenOrderAndContactExist_ReturnsEmail()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var uri = req.RequestUri!.ToString();
                if (uri.Contains("kb_order/456"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"id\": 456, \"contact_id\": 999}", System.Text.Encoding.UTF8, "application/json")
                    });
                }
                if (uri.Contains("contact/999"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"id\": 999, \"mail\": \"client@domain.com\"}", System.Text.Encoding.UTF8, "application/json")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var email = await client.GetOrderContactEmailAsync(456);

        // Assert
        email.Should().Be("client@domain.com");
    }

    [Fact]
    public async Task PreFetchArticlesAsync_CachesArticles_SubsequentLookupsUseCache()
    {
        // Arrange
        int apiCallCount = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                apiCallCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 500, \"intern_code\": \"FS27ART-100Black\", \"intern_name\": \"FS27 Sample Black\"}]", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act - PreFetch
        await client.PreFetchArticlesAsync("FS27", ["ART-100"]);
        int initialCallCount = apiCallCount;

        // Act - Find Article (should hit cache)
        var article = await client.FindArticleAsync("ART-100", "140 Black", "FS27");

        // Assert
        article.Should().NotBeNull();
        article!.Id.Should().Be(500);
        apiCallCount.Should().Be(initialCallCount); // No additional API calls made
    }

    [Fact]
    public async Task AddDiscountPositionAsync_WhenCalled_SendsCorrectPayloadToBexioApi()
    {
        // Arrange
        string? requestUri = null;
        string? requestBody = null;

        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                requestUri = req.RequestUri?.ToString();
                if (req.Content != null)
                {
                    requestBody = await req.Content.ReadAsStringAsync();
                }
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\": 999}", Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        await client.AddDiscountPositionAsync(12345, 10m, "Sonderrabatt (10%)");

        // Assert
        requestUri.Should().Be("https://api.bexio.com/2.0/kb_order/12345/kb_position_discount");
        requestBody.Should().NotBeNull();
        requestBody.Should().Contain("Sonderrabatt (10%)");
        requestBody.Should().Contain("\"value\":10");
        requestBody.Should().Contain("\"is_percentual\":true");
    }

    [Fact]
    public async Task RateLimit_When429Returned_ShouldWaitAndRetryRequest()
    {
        // Arrange
        int callCount = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var rateLimitResp = new HttpResponseMessage((HttpStatusCode)429);
                    rateLimitResp.Headers.Add("ratelimit-remaining", "0");
                    rateLimitResp.Headers.Add("ratelimit-reset", "1");
                    return Task.FromResult(rateLimitResp);
                }

                var successResp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 555}]", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(successResp);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindContactIdAsync("test@retry.com");

        // Assert
        result.Should().Be(555);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task CheckConnectionAsync_WhenSuccess_ShouldReturnTrue()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\":1}]", Encoding.UTF8, "application/json")
                });
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        bool result = await client.CheckConnectionAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckConnectionAsync_WhenExceptionThrown_ShouldReturnFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => throw new HttpRequestException("Network failure")
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        bool result = await client.CheckConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrderContactEmailAsync_WhenContactFound_ShouldReturnEmail()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var uri = req.RequestUri!.ToString();
                if (uri.Contains("kb_order/99"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"id\":99, \"contact_id\":123}", Encoding.UTF8, "application/json")
                    });
                }
                if (uri.Contains("contact/123"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"id\":123, \"mail\":\"buyer@bexio.com\"}", Encoding.UTF8, "application/json")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        string? email = await client.GetOrderContactEmailAsync(99);

        // Assert
        email.Should().Be("buyer@bexio.com");
    }

    [Fact]
    public async Task FindContactIdAsync_WhenRateLimited429_ShouldRetryAndSucceed()
    {
        // Arrange
        int attempt = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                attempt++;
                if (attempt == 1)
                {
                    var response429 = new HttpResponseMessage((HttpStatusCode)429);
                    response429.Headers.Add("ratelimit-remaining", "0");
                    response429.Headers.Add("ratelimit-reset", "1");
                    return Task.FromResult(response429);
                }
                var response200 = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 555}]", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response200);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        // Act
        var result = await client.FindContactIdAsync("retry@company.com");

        // Assert
        attempt.Should().Be(2);
        result.Should().Be(555);
    }

    [Fact]
    public async Task GetAccountsAsync_ShouldCacheResultsOnConsecutiveCalls()
    {
        int callCount = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                callCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 1, \"account_type\": 1, \"is_active\": true}]", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        var firstCall = await client.GetAccountsAsync();
        var secondCall = await client.GetAccountsAsync();

        callCount.Should().Be(1);
        firstCall.Should().BeSameAs(secondCall);
        firstCall.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTaxesAsync_ShouldCacheResultsOnConsecutiveCalls()
    {
        int callCount = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                callCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 10, \"type\": \"sales_tax\", \"is_active\": true}]", Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        var firstCall = await client.GetTaxesAsync();
        var secondCall = await client.GetTaxesAsync();

        callCount.Should().Be(1);
        firstCall.Should().BeSameAs(secondCall);
        firstCall.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateContactAsync_WhenApiReturnsEmptyJson_ShouldThrowInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            })
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var customer = new Customer { CompanyName = "Test Co", Email = "test@co.com" };

        Func<Task> act = async () => await client.CreateContactAsync(customer);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Bexio returned an empty response when creating a contact.");
    }

    [Fact]
    public async Task CreateOrderAsync_WhenApiReturnsEmptyJson_ShouldThrowInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            })
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var order = new Order { Customer = new Customer { CompanyName = "Test Co" } };

        Func<Task> act = async () => await client.CreateOrderAsync(1, order);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Bexio returned an empty response when creating an order.");
    }

    [Fact]
    public async Task FindContactIdAsync_WhenRateLimitHeaderUsesUnixTimestamp_ShouldHandleCorrectly()
    {
        int callCount = 0;
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                callCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\": 888}]", Encoding.UTF8, "application/json")
                };
                long futureUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1;
                response.Headers.Add("ratelimit-remaining", "0");
                response.Headers.Add("ratelimit-reset", futureUnix.ToString());
                return Task.FromResult(response);
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);

        var id = await client.FindContactIdAsync("unix@test.com");
        id.Should().Be(888);
        callCount.Should().Be(1);
    }
}
