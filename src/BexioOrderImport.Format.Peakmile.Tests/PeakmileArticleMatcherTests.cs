using System.Net;
using System.Text;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Format.Peakmile.Tests.Utils;
using BexioOrderImport.Infrastructure.Bexio;
using FluentAssertions;

namespace BexioOrderImport.Format.Peakmile.Tests;

/// <summary>
/// The Peakmile article lookup against the real Bexio client with a mocked HTTP handler.
/// These cases tested BexioApiClient.FindArticleAsync before the matching moved into the format.
/// </summary>
public class PeakmileArticleMatcherTests
{    [Test]
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
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27"))).Article;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(77777);
        result.Name.Should().Be("Sample Product Name");
        result.Description.Should().Be("Sample Product Description");
    }

    [Test]
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
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27"))).Article;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(77777);
        result.Name.Should().Be("FS27 Sample Product Name Black");
        result.Description.Should().Be("Sample Product Description");
    }

    [Test]
    public async Task FindArticleAsync_WithUnknownArticle_ShouldNotMatch()
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
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = await matcher.FindArticleAsync(new("ART-01", "Black", "FS27"));

        // Assert
        result.Should().Be(new ArticleMatch(null, "not found"));
    }

    [Test]
    public async Task FindArticleAsync_WhenSeasonCodeMisses_ShouldMatchFallbackCodesInOrder()
    {
        // Arrange
        var requestBodies = new List<string>();
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                string requestBody = await req.Content!.ReadAsStringAsync(token);
                requestBodies.Add(requestBody);
                string json = requestBody.Contains("\"EXART-001Black\"")
                    ? "[{\"id\": 555, \"intern_code\": \"EXART-001Black\", \"intern_name\": \"Sample Product Name\"}]"
                    : "[]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27", ["NOOS", "EX"]))).Article;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(555);
        requestBodies.Should().HaveCount(3);
        requestBodies[0].Should().Contain("\"FS27ART-001Black\"");
        requestBodies[1].Should().Contain("\"NOOSART-001Black\"");
        requestBodies[2].Should().Contain("\"EXART-001Black\"");
    }

    [Test]
    public async Task FindArticleAsync_WhenInternCodeHasSpaces_ShouldMatchItThroughTheArticleNumberSearch()
    {
        // Arrange: the exact lookups miss, the like search by article number returns the codes with spaces
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                string requestBody = await req.Content!.ReadAsStringAsync(token);
                string json = requestBody.Contains("\"like\"")
                    ? "[{\"id\": 1, \"intern_code\": \"NOOS 5440 White\", \"intern_name\": \"Jacket White\"}," +
                      "{\"id\": 2, \"intern_code\": \"NOOS 5440 Black\", \"intern_name\": \"Jacket Black\"}]"
                    : "[]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var client = new BexioApiClient(new HttpClient(handler), "dummy-token", 1, 1);
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = (await matcher.FindArticleAsync(new("5440", "140 Black", "FS27", ["NOOS"]))).Article;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
    }

    [Test]
    public async Task FindArticleAsync_WhenSeasonCodeMatches_ShouldNotQueryFallbackCodes()
    {
        // Arrange
        var requestBodies = new List<string>();
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                string requestBody = await req.Content!.ReadAsStringAsync(token);
                requestBodies.Add(requestBody);
                string json = requestBody.Contains("\"FS27ART-001Black\"")
                    ? "[{\"id\": 111, \"intern_code\": \"FS27ART-001Black\", \"intern_name\": \"Season Product\"}]"
                    : "[{\"id\": 555, \"intern_code\": \"EXART-001Black\", \"intern_name\": \"Fallback Product\"}]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27", ["EX"]))).Article;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(111);
        requestBodies.Should().ContainSingle();
    }

    [Test]
    public async Task FindArticleAsync_WhenNoCodeMatchesExactly_ShouldFilterByNameWithSeasonCodeOnly()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                string requestBody = await req.Content!.ReadAsStringAsync(token);
                // Only the "like" search by article number returns articles, both exact intern code searches miss
                string json = requestBody.Contains("\"ART-001\"")
                    ? "[" +
                      "{\"id\": 555, \"intern_code\": \"X1ART-001\", \"intern_name\": \"EX Sample Product Name Black\"}," +
                      "{\"id\": 111, \"intern_code\": \"X2ART-001\", \"intern_name\": \"FS27 Sample Product Name Black\"}]"
                    : "[]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var matcher = new PeakmileArticleMatcher(client);

        // Act
        var result = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27", ["EX"]))).Article;

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(111);
    }

    [Test]
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
        var matcher = new PeakmileArticleMatcher(client);

        // Act - PreFetch
        await matcher.PreFetchArticlesAsync("FS27", ["ART-100"]);
        int initialCallCount = apiCallCount;

        // Act - Find Article (should hit cache)
        var article = (await matcher.FindArticleAsync(new("ART-100", "140 Black", "FS27"))).Article;

        // Assert
        article.Should().NotBeNull();
        article!.Id.Should().Be(500);
        apiCallCount.Should().Be(initialCallCount); // No additional API calls made
    }

    [Test]
    public async Task PreFetchArticlesAsync_WhenBulkSearchEmpty_ShouldFallbackToIndividualSearch()
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                string uri = req.RequestUri!.ToString();
                if (uri.Contains("article/search"))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[{\"id\": 101, \"intern_code\": \"ART-999\", \"intern_name\": \"Fallback Article\"}]", Encoding.UTF8, "application/json")
                    });
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });
            }
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var matcher = new PeakmileArticleMatcher(client);

        await matcher.PreFetchArticlesAsync("SS26", ["ART-999"]);

        var article = (await matcher.FindArticleAsync(new("ART-999", "Red", "SS26"))).Article;
        article.Should().NotBeNull();
        article!.Id.Should().Be(101);
    }

    [Test]
    public async Task FindArticleByArticleNumberAndFilterAsync_WithMultipleMatchesAndFilterFallback_ReturnsMatchingArticle()
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\": 10, \"intern_code\": \"ART123\", \"intern_name\": \"Shirt Blue SS26\"}, {\"id\": 20, \"intern_code\": \"ART123\", \"intern_name\": \"Shirt Red FW26\"}]", Encoding.UTF8, "application/json")
            })
        };

        var httpClient = new HttpClient(handler);
        var client = new BexioApiClient(httpClient, "dummy-token", 1, 1);
        var matcher = new PeakmileArticleMatcher(client);

        var article = (await matcher.FindArticleAsync(new("ART123", "Red", "FW26"))).Article;
        article.Should().NotBeNull();
        article!.Id.Should().Be(20);
    }

    [Test]
    public async Task FindArticleAsync_WhenCriteriaIsNull_ShouldThrow()
    {
        var matcher = new PeakmileArticleMatcher(new BexioApiClient(new HttpClient(), "dummy-token", 1, 1));

        await ((Func<Task>)(() => matcher.FindArticleAsync(null!))).Should().ThrowAsync<ArgumentNullException>();
    }
}
