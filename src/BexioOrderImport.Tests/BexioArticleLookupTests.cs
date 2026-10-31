using System.Net;
using System.Text;
using System.Text.Json;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Tests;

/// <summary>
/// Exact intern_code lookup (used for EANs) and the sale price of articles.
/// </summary>
public class BexioArticleLookupTests
{
    private static (BexioApiClient Client, List<string> Requests) Client(string responseJson)
    {
        var requests = new List<string>();
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                requests.Add(req.Content == null ? string.Empty : await req.Content.ReadAsStringAsync(token));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
            }
        };
        return (new BexioApiClient(new HttpClient(handler), "dummy-token", 1, 1), requests);
    }

    [Test]
    public async Task FindArticlesByInternCodeAsync_ShouldSearchTheExactCodeAndCacheTheResult()
    {
        var (client, requests) = Client("""[{ "id": 1, "intern_code": "4006381333931", "intern_name": "Shirt", "sale_price": "12.50" }]""");

        var first = await client.FindArticlesByInternCodeAsync(" 4006381333931 ");
        var second = await client.FindArticlesByInternCodeAsync("4006381333931");

        first.Should().ContainSingle().Which.SalePrice.Should().Be(12.5m);
        second.Should().ContainSingle().Which.Id.Should().Be(1);
        requests.Should().ContainSingle("the second lookup comes from the cache");
        requests[0].Should().Contain("\"intern_code\"").And.Contain("\"4006381333931\"").And.Contain("\"=\"");
    }

    [Test]
    public async Task FindArticlesByInternCodeAsync_ShouldReturnAllMatches()
    {
        var (client, _) = Client("""[{ "id": 1, "intern_code": "123" }, { "id": 2, "intern_code": "123" }]""");

        var articles = await client.FindArticlesByInternCodeAsync("123");

        articles.Select(a => a.Id).Should().Equal(1, 2);
    }

    [Test]
    public async Task FindArticlesByInternCodeAsync_WhenNothingMatches_ShouldReturnEmpty()
    {
        var (client, _) = Client("[]");

        var articles = await client.FindArticlesByInternCodeAsync("123");

        articles.Should().BeEmpty();
    }

    [Test]
    public async Task FindArticlesByInternCodeAsync_WhenCodeIsEmpty_ShouldNotCallBexio()
    {
        var (client, requests) = Client("[]");

        var articles = await client.FindArticlesByInternCodeAsync(" ");

        articles.Should().BeEmpty();
        requests.Should().BeEmpty();
    }

    [Test]
    public async Task SearchArticlesAsync_ShouldPostTheCriteriaAndNotCache()
    {
        var (client, requests) = Client("""[{ "id": 1, "intern_code": "FS27ART" }]""");
        BexioSearchCriteria[] criteria = [new() { Field = "intern_code", Value = "ART", Criteria = "like" }];

        var first = await client.SearchArticlesAsync(criteria);
        await client.SearchArticlesAsync(criteria);

        first.Should().ContainSingle().Which.Id.Should().Be(1);
        requests.Should().HaveCount(2);
        requests[0].Should().Contain("\"like\"").And.Contain("\"ART\"");
    }

    [Test]
    public async Task SearchArticlesAsync_ShouldLoadPagesUntilAPageIsNotFull()
    {
        var uris = new List<string>();
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                uris.Add(req.RequestUri!.PathAndQuery);
                // A full first page of 2000 articles, then a page with one more
                int count = uris.Count == 1 ? 2000 : 1;
                string json = JsonSerializer.Serialize(Enumerable.Range(uris.Count * 10000, count).Select(id => new { id, intern_code = $"FS26{id}" }));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
        };
        var client = new BexioApiClient(new HttpClient(handler), "dummy-token", 1, 1);

        var articles = await client.SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = "FS26", Criteria = "like" }]);

        articles.Should().HaveCount(2001);
        uris.Should().Equal("/2.0/article/search?limit=2000&offset=0", "/2.0/article/search?limit=2000&offset=2000");
    }

    [Test]
    public async Task SearchArticlesAsync_WhenBexioFails_ShouldReturnEmpty()
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
        };
        var client = new BexioApiClient(new HttpClient(handler), "dummy-token", 1, 1);

        var articles = await client.SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = "X", Criteria = "like" }]);

        articles.Should().BeEmpty();
    }

    [Test]
    [Arguments("12.5", "12.5")]
    [Arguments("\"12.50\"", "12.5")]
    [Arguments("null", null)]
    [Arguments("\"\"", null)]
    public void SalePrice_ShouldReadNumbersAndStrings(string json, string? expected)
    {
        var article = JsonSerializer.Deserialize<BexioArticle>($$"""{ "id": 1, "sale_price": {{json}} }""")!;

        article.SalePrice.Should().Be(expected == null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public void SalePrice_ShouldBeWrittenAsNumberOrNull()
    {
        JsonSerializer.Serialize(new BexioArticle { SalePrice = 9.9m }).Should().Contain("\"sale_price\":9.9");
        JsonSerializer.Serialize(new BexioArticle()).Should().Contain("\"sale_price\":null");
    }
}
