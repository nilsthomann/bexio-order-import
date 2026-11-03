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
    private readonly List<string> _searches = [];

    /// <summary>
    /// Matcher whose Bexio client answers each article search with the articles whose intern code contains the searched value.
    /// </summary>
    private SgdArticleMatcher Matcher(params (int Id, string? Code)[] articles)
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = async (req, token) =>
            {
                string body = await req.Content!.ReadAsStringAsync(token);
                string value = System.Text.Json.JsonDocument.Parse(body).RootElement[0].GetProperty("value").GetString()!;
                _searches.Add(value);
                // An article without intern code is returned by every search
                var found = articles.Where(a => a.Code == null || a.Code.Contains(value, StringComparison.OrdinalIgnoreCase));
                string json = System.Text.Json.JsonSerializer.Serialize(found.Select(a => new { id = a.Id, intern_code = a.Code, intern_name = $"Article {a.Id}" }));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            }
        };
        return new SgdArticleMatcher(new BexioApiClient(new HttpClient(handler), "dummy-token", 1, 1));
    }

    private static SgdArticleCriteria Criteria(string color = "1001 Black", params string[] fallbackCodes) => new("E183801", color, "FS26", fallbackCodes);

    [Test]
    [Arguments("FS26E1838011001 Black")]
    [Arguments("FS26E1838011001Black")]
    [Arguments("FS26 E183801 1001 Black")]
    [Arguments("FS26 E183801 1001Black")]
    [Arguments("FS26E183801 Black")]
    [Arguments("FS26E183801Black")]
    [Arguments("FS26 E183801 Black")]
    [Arguments("FS26 E183801Black")]
    [Arguments("fs26 e183801black")]
    [Arguments("FS26E1838011001")]
    [Arguments("FS26 E183801 1001")]
    public async Task FindArticleAsync_ShouldMatchTheInternCodeWithOrWithoutSpacesColorCodeOrColorName(string internCode)
    {
        var matcher = Matcher((7, internCode), (8, "FS26E183801 White"), (9, "FS25E183801 Black"));

        await matcher.PreFetchArticlesAsync("FS26", ["E183801"]);
        var match = await matcher.FindArticleAsync(Criteria());

        match.Article!.Id.Should().Be(7);
        _searches.Should().Equal("FS26");
    }

    [Test]
    public async Task FindArticleAsync_ShouldNeedOneRequestForAllPositionsOfAnImport()
    {
        var matcher = Matcher((1, "FS26 E183801 1001 Black"), (2, "FS26 E183801 2048 Navy"), (3, "NOOS E200 Red"));

        await matcher.PreFetchArticlesAsync("FS26", ["E183801", "E200"]);
        await matcher.PreFetchArticlesAsync("NOOS", ["E183801", "E200"]);
        var black = await matcher.FindArticleAsync(Criteria());
        var navy = await matcher.FindArticleAsync(Criteria("2048 Navy"));
        var red = await matcher.FindArticleAsync(new("E200", "Red", "FS26", ["NOOS"]));
        await matcher.FindArticleAsync(Criteria());

        (black.Article!.Id, navy.Article!.Id, red.Article!.Id).Should().Be((1, 2, 3));
        _searches.Should().Equal("FS26", "NOOS");
    }

    [Test]
    public async Task FindArticleAsync_WithoutPreFetch_ShouldSearchTheArticleNumberOnce()
    {
        var matcher = Matcher((1, "FS26E183801Black"), (2, "FS26E183801White"));

        var black = await matcher.FindArticleAsync(Criteria());
        var white = await matcher.FindArticleAsync(Criteria("White"));
        var red = await matcher.FindArticleAsync(Criteria("Red"));
        await matcher.FindArticleAsync(Criteria("Red"));

        (black.Article!.Id, white.Article!.Id).Should().Be((1, 2));
        red.Should().Be(ArticleMatch.NotFound());
        _searches.Should().Equal("E183801");
    }

    [Test]
    public async Task FindArticleAsync_WhenThePreFetchedSeasonHasNoMatch_ShouldSearchTheArticleNumber()
    {
        var matcher = Matcher((1, "FS26 E999 Black"), (2, "NOOS E183801 Black"));

        await matcher.PreFetchArticlesAsync("FS26", ["E183801"]);
        var match = await matcher.FindArticleAsync(Criteria("1001 Black", "NOOS"));

        match.Article!.Id.Should().Be(2);
        _searches.Should().Equal("FS26", "E183801");
    }

    [Test]
    public async Task FindArticleAsync_ShouldPreferTheFullColorAndTheSeasonCode()
    {
        var matcher = Matcher((1, "NOOSE1838011001Black"), (2, "FS26E183801Black"), (3, "FS26E1838011001Black"), (4, "FS26E1838011001"));

        var match = await matcher.FindArticleAsync(Criteria("1001 Black", "NOOS"));

        match.Article!.Id.Should().Be(3);
    }

    [Test]
    public async Task FindArticleAsync_ShouldPreferTheColorNameOverTheColorCode()
    {
        var matcher = Matcher((1, "FS26E1838011001"), (2, "FS26E183801Black"));

        var match = await matcher.FindArticleAsync(Criteria());

        match.Article!.Id.Should().Be(2);
    }

    [Test]
    public async Task FindArticleAsync_WhenTheSeasonCodeMisses_ShouldMatchTheFallbackCodesInOrder()
    {
        var matcher = Matcher((1, "EX E183801 Black"), (2, "NOOS E183801 Black"));

        var match = await matcher.FindArticleAsync(Criteria("1001 Black", "NOOS", "EX"));

        match.Article!.Id.Should().Be(2);
    }

    [Test]
    public async Task FindArticleAsync_WhenSeveralArticlesHaveTheCode_ShouldNotBeUnique()
    {
        var matcher = Matcher((1, "FS26 E183801 Black"), (2, "FS26E183801Black"));

        var match = await matcher.FindArticleAsync(Criteria());

        match.Should().Be(ArticleMatch.NotFound("not unique (2 articles)"));
    }

    [Test]
    public async Task FindArticleAsync_ShouldNotGuessAnArticleOfAnotherColorOrSeason()
    {
        var matcher = Matcher((1, "FS26E183801White"), (2, "FS25E183801Black"), (3, null));

        var match = await matcher.FindArticleAsync(Criteria());

        match.Should().Be(ArticleMatch.NotFound());
    }

    [Test]
    public async Task FindArticleAsync_WhenTheColorIsOnlyACode_ShouldNotMatchTheArticleWithoutColor()
    {
        var matcher = Matcher((1, "FS26E183801"), (2, "FS26 E183801 1001"));

        var match = await matcher.FindArticleAsync(Criteria("1001"));

        match.Article!.Id.Should().Be(2);
    }

    [Test]
    public async Task PreFetchArticlesAsync_ShouldSearchEachSeasonCodeOnceAndOnlyWithArticleNumbers()
    {
        var matcher = Matcher((1, "FS26E183801Black"));

        await matcher.PreFetchArticlesAsync("FS26", [null!, " "]);
        await matcher.PreFetchArticlesAsync(" ", ["E183801"]);
        await matcher.PreFetchArticlesAsync("FS26", ["E183801"]);
        await matcher.PreFetchArticlesAsync("fs26 ", ["E183801"]);

        _searches.Should().Equal("FS26");
    }

    [Test]
    public async Task ArgumentsShouldNotBeNull()
    {
        var matcher = Matcher();

        await ((Func<Task>)(() => matcher.FindArticleAsync(null!))).Should().ThrowAsync<ArgumentNullException>();
        await ((Func<Task>)(() => matcher.PreFetchArticlesAsync("FS26", null!))).Should().ThrowAsync<ArgumentNullException>();
        ((Action)(() => _ = new SgdArticleMatcher(null!))).Should().Throw<ArgumentNullException>();
    }
}
