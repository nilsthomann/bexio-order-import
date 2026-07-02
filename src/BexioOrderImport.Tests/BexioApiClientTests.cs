using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Infrastructure.Bexio;

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
}
