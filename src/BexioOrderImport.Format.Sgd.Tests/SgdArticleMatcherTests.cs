using System.Net;
using System.Text;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Format.Sgd.Tests.Utils;
using BexioOrderImport.Infrastructure.Bexio;
using FluentAssertions;

namespace BexioOrderImport.Format.Sgd.Tests;

/// <summary>
/// The SGD article lookup against the real Bexio client with a mocked HTTP handler; no request leaves the test.
/// </summary>
public class SgdArticleMatcherTests
{
    private readonly List<string> _requestBodies = [];

    /// <summary>
    /// Matcher whose Bexio client answers each request with the JSON returned for its body.
    /// </summary>
    private SgdArticleMatcher Matcher(Func<string, string> respond)
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                string body = req.Content == null ? string.Empty : await req.Content.ReadAsStringAsync(token);
                _requestBodies.Add(body);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(respond(body), Encoding.UTF8, "application/json") };
            }
        };
        return new SgdArticleMatcher(new BexioApiClient(new HttpClient(handler), "dummy-token", 1, 1));
    }

    [Test]
    public async Task FindArticleAsync_ShouldMatchTheInternCodeOfSeasonArticleNumberAndColorName()
    {
        var matcher = Matcher(body => body.Contains("\"FS27E231001Black\"")
            ? "[{\"id\": 7, \"intern_code\": \"FS27E231001Black\", \"intern_name\": \"Run Jacket\", \"intern_description\": \"Light\"}]"
            : "[]");

        var article = (await matcher.FindArticleAsync(new("E231001", "1001 Black", "FS27"))).Article;

        article.Should().NotBeNull();
        article!.Id.Should().Be(7);
        article.Name.Should().Be("Run Jacket");
        _requestBodies.Should().ContainSingle("the color code 1001 is not part of the intern code");
    }

    [Test]
    public async Task FindArticleAsync_WhenSeasonCodeMisses_ShouldMatchFallbackCodesInOrder()
    {
        var matcher = Matcher(body => body.Contains("\"EXART-001Black\"")
            ? "[{\"id\": 555, \"intern_code\": \"EXART-001Black\", \"intern_name\": \"Sample\"}]"
            : "[]");

        var article = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27", ["NOOS", "EX"]))).Article;

        article!.Id.Should().Be(555);
        _requestBodies.Should().HaveCount(3);
        _requestBodies[0].Should().Contain("\"FS27ART-001Black\"");
        _requestBodies[1].Should().Contain("\"NOOSART-001Black\"");
        _requestBodies[2].Should().Contain("\"EXART-001Black\"");
    }

    [Test]
    public async Task FindArticleAsync_WhenNoCodeMatchesExactly_ShouldFilterByNameWithSeasonCodeOnly()
    {
        var matcher = Matcher(body => body.Contains("\"ART-001\"")
            ? "[{\"id\": 555, \"intern_code\": \"X1ART-001\", \"intern_name\": \"EX Sample Black\"}," +
              "{\"id\": 111, \"intern_code\": \"X2ART-001\", \"intern_name\": \"FS27 Sample Black\"}]"
            : "[]");

        var article = (await matcher.FindArticleAsync(new("ART-001", "Black", "FS27", ["EX"]))).Article;

        article!.Id.Should().Be(111);
    }

    [Test]
    public async Task FindArticleAsync_WhenTheArticleNumberSearchFindsOneArticle_ShouldUseIt()
    {
        var matcher = Matcher(body => body.Contains("\"like\"")
            ? "[{\"id\": 9, \"intern_code\": \"OTHERART-9\", \"intern_name\": \"Without season\"}]"
            : "[]");

        var article = (await matcher.FindArticleAsync(new("ART-9", "Red", "FS27"))).Article;

        article!.Id.Should().Be(9);
    }

    [Test]
    public async Task FindArticleAsync_WithUnknownArticle_ShouldNotMatch()
    {
        var matcher = Matcher(_ => "[]");

        var result = await matcher.FindArticleAsync(new("ART-01", "Black", "FS27"));

        result.Should().Be(new ArticleMatch(null, "not found"));
    }

    [Test]
    public async Task PreFetchArticlesAsync_ShouldCacheTheArticlesOfTheSeasonForTheLookups()
    {
        var matcher = Matcher(_ => "[{\"id\": 500, \"intern_code\": \"FS27ART-100Black\", \"intern_name\": \"FS27 Sample Black\"}," +
                                   "{\"id\": 501, \"intern_code\": \"FS27OTHERBlack\", \"intern_name\": \"Not ordered\"}]");

        await matcher.PreFetchArticlesAsync("FS27", ["ART-100", " ", "ART-100"]);
        int requests = _requestBodies.Count;
        var article = (await matcher.FindArticleAsync(new("ART-100", "140 Black", "FS27"))).Article;

        article!.Id.Should().Be(500);
        _requestBodies.Should().HaveCount(requests, "the lookup uses the cache");
    }

    [Test]
    public async Task PreFetchArticlesAsync_WhenTheSeasonSearchIsEmpty_ShouldSearchEachArticleNumber()
    {
        var matcher = Matcher(body => body.Contains("\"ART-999\"")
            ? "[{\"id\": 101, \"intern_code\": \"ART-999\", \"intern_name\": \"Fallback Article\"}]"
            : "[]");

        await matcher.PreFetchArticlesAsync("SS26", ["ART-999"]);
        await matcher.PreFetchArticlesAsync(string.Empty, ["ART-999"]);

        _requestBodies.Count(b => b.Contains("\"ART-999\"")).Should().Be(1, "an article number already in the cache is not searched again");
        (await matcher.FindArticleAsync(new("ART-999", "Red", "SS26"))).Article!.Id.Should().Be(101);
    }

    [Test]
    public async Task PreFetchArticlesAsync_WithoutArticleNumbers_ShouldNotCallBexio()
    {
        var matcher = Matcher(_ => "[]");

        await matcher.PreFetchArticlesAsync("FS27", [null!, ""]);

        _requestBodies.Should().BeEmpty();
    }

    [Test]
    public async Task FindArticleAsync_WhenCriteriaIsNull_ShouldThrow()
    {
        var matcher = Matcher(_ => "[]");

        await ((Func<Task>)(() => matcher.FindArticleAsync(null!))).Should().ThrowAsync<ArgumentNullException>();
    }

    [Test]
    public void Constructor_WhenBexioIsNull_ShouldThrow()
    {
        Action act = () => _ = new SgdArticleMatcher(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
