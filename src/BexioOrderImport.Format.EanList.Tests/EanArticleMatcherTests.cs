using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Format.EanList.Tests;

public class EanArticleMatcherTests
{
    private readonly Mock<IBexioService> _bexio = new();

    private void SetupArticles(params int[] ids)
        => _bexio.Setup(b => b.FindArticlesByInternCodeAsync("4006381333931")).ReturnsAsync([.. ids.Select(id => new BexioArticle { Id = id })]);

    /// <summary>
    /// Bexio articles whose intern_code contains the searched value, as a "like" search returns them.
    /// </summary>
    private void SetupLikeSearch(string value, params string[] codes)
        => _bexio.Setup(b => b.SearchArticlesAsync(It.Is<IReadOnlyList<BexioSearchCriteria>>(c =>
                c.Count == 1 && c[0].Field == "intern_code" && c[0].Criteria == "like" && c[0].Value == value)))
            .ReturnsAsync([.. codes.Select((code, i) => new BexioArticle { Id = i + 1, Code = code })]);

    [Test]
    public async Task FindArticleAsync_WithOneArticle_ShouldMatchIt()
    {
        SetupArticles(7);

        var match = await new EanArticleMatcher(_bexio.Object).FindArticleAsync(new("4006381333931"));

        match.IsFound.Should().BeTrue();
        match.Article!.Id.Should().Be(7);
        match.FailureReason.Should().BeNull();
    }

    [Test]
    [Arguments(new int[0], "not found")]
    [Arguments(new[] { 1, 2 }, "not unique (2 articles)")]
    public async Task FindArticleAsync_WithoutExactlyOneArticle_ShouldNotMatch(int[] ids, string reason)
    {
        SetupArticles(ids);

        var match = await new EanArticleMatcher(_bexio.Object).FindArticleAsync(new("4006381333931"));

        match.IsFound.Should().BeFalse();
        match.Article.Should().BeNull();
        match.FailureReason.Should().Be(reason);
    }

    [Test]
    public async Task FindArticleAsync_WhenArgumentIsNull_ShouldThrow()
    {
        await ((Func<Task>)(() => new EanArticleMatcher(_bexio.Object).FindArticleAsync(null!))).Should().ThrowAsync<ArgumentNullException>();
        ((Action)(() => _ = new EanArticleMatcher(null!))).Should().Throw<ArgumentNullException>();
    }

    [Test]
    [Arguments("826710896036", "0826710896036")]
    [Arguments("0826710896036", "826710896036")]
    [Arguments("00826710896036", "0826710896036")]
    public async Task LeadingZeroMatcher_ShouldMatchTheCodeApartFromLeadingZeros(string ean, string internCode)
    {
        SetupLikeSearch("826710896036", internCode, "18267108960361", "8267108960369");

        var match = await new LeadingZeroEanArticleMatcher(_bexio.Object).FindArticleAsync(new(ean));

        match.Article!.Code.Should().Be(internCode, "codes that only contain the EAN are no match");
    }

    [Test]
    public async Task LeadingZeroMatcher_WhenSeveralCodesDifferOnlyInLeadingZeros_ShouldNotBeUnique()
    {
        SetupLikeSearch("826710896036", "0826710896036", "00826710896036");

        var match = await new LeadingZeroEanArticleMatcher(_bexio.Object).FindArticleAsync(new("826710896036"));

        match.FailureReason.Should().Be("not unique (2 articles)");
    }

    [Test]
    public async Task LeadingZeroMatcher_WhenNoCodeIsEqual_ShouldNotFindTheArticle()
    {
        SetupLikeSearch("826710896036", "18267108960361");

        var match = await new LeadingZeroEanArticleMatcher(_bexio.Object).FindArticleAsync(new("826710896036"));

        match.IsMissing.Should().BeTrue();
    }

    [Test]
    public async Task LeadingZeroMatcher_WithOnlyZeros_ShouldNotSearch()
    {
        var match = await new LeadingZeroEanArticleMatcher(_bexio.Object).FindArticleAsync(new("0000"));

        match.IsMissing.Should().BeTrue();
        _bexio.VerifyNoOtherCalls();
    }

    [Test]
    public async Task LeadingZeroMatcher_WhenArgumentIsNull_ShouldThrow()
    {
        await ((Func<Task>)(() => new LeadingZeroEanArticleMatcher(_bexio.Object).FindArticleAsync(null!))).Should().ThrowAsync<ArgumentNullException>();
        ((Action)(() => _ = new LeadingZeroEanArticleMatcher(null!))).Should().Throw<ArgumentNullException>();
    }

    [Test]
    public async Task WithFallbacks_WhenTheExactCodeIsMissing_ShouldIgnoreLeadingZeros()
    {
        _bexio.Setup(b => b.FindArticlesByInternCodeAsync("826710896036")).ReturnsAsync([]);
        SetupLikeSearch("826710896036", "0826710896036");

        var match = await EanArticleMatcher.WithFallbacks(_bexio.Object).FindArticleAsync(new("826710896036"));

        match.Article!.Code.Should().Be("0826710896036");
    }

    [Test]
    public async Task WithFallbacks_WhenTheExactCodeExists_ShouldNotSearchFurther()
    {
        SetupArticles(7);

        var match = await EanArticleMatcher.WithFallbacks(_bexio.Object).FindArticleAsync(new("4006381333931"));

        match.Article!.Id.Should().Be(7);
        _bexio.Verify(b => b.SearchArticlesAsync(It.IsAny<IReadOnlyList<BexioSearchCriteria>>()), Times.Never);
    }
}
