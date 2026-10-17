using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class FallbackArticleMatcherTests
{
    private readonly Mock<IArticleMatcher<string>> _first = new();
    private readonly Mock<IArticleMatcher<string>> _second = new();

    [Test]
    public async Task FindArticleAsync_WhenTheFirstMatcherFindsTheArticle_ShouldNotRunTheFallback()
    {
        var article = new BexioArticle { Id = 1 };
        _first.Setup(m => m.FindArticleAsync("x")).ReturnsAsync(ArticleMatch.Found(article));

        var match = await new FallbackArticleMatcher<string>(_first.Object, _second.Object).FindArticleAsync("x");

        match.Article.Should().BeSameAs(article);
        _second.Verify(m => m.FindArticleAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task FindArticleAsync_WhenTheFirstMatcherFindsNothing_ShouldUseTheFallback()
    {
        var article = new BexioArticle { Id = 2 };
        _first.Setup(m => m.FindArticleAsync("x")).ReturnsAsync(ArticleMatch.NotFound());
        _second.Setup(m => m.FindArticleAsync("x")).ReturnsAsync(ArticleMatch.Found(article));

        var match = await new FallbackArticleMatcher<string>(_first.Object, _second.Object).FindArticleAsync("x");

        match.Article.Should().BeSameAs(article);
    }

    [Test]
    public async Task FindArticleAsync_WhenTheFirstMatchIsNotUnique_ShouldStopWithoutFallback()
    {
        _first.Setup(m => m.FindArticleAsync("x")).ReturnsAsync(ArticleMatch.FromCandidates([new BexioArticle(), new BexioArticle()]));

        var match = await new FallbackArticleMatcher<string>(_first.Object, _second.Object).FindArticleAsync("x");

        match.Should().Be(new ArticleMatch(null, "not unique (2 articles)"));
        _second.Verify(m => m.FindArticleAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task FindArticleAsync_WhenNoMatcherFindsTheArticle_ShouldReturnNotFound()
    {
        _first.Setup(m => m.FindArticleAsync("x")).ReturnsAsync(ArticleMatch.NotFound());
        _second.Setup(m => m.FindArticleAsync("x")).ReturnsAsync(ArticleMatch.NotFound());

        var match = await new FallbackArticleMatcher<string>(_first.Object, _second.Object).FindArticleAsync("x");

        match.IsMissing.Should().BeTrue();
        match.FailureReason.Should().Be(ArticleMatch.NotFoundReason);
    }

    [Test]
    public void Constructor_WithoutMatchers_ShouldThrow()
    {
        ((Action)(() => _ = new FallbackArticleMatcher<string>(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => _ = new FallbackArticleMatcher<string>())).Should().Throw<ArgumentException>();
    }

    [Test]
    public void IsMissing_ShouldOnlyBeTrueWithoutAnyArticle()
    {
        ArticleMatch.NotFound().IsMissing.Should().BeTrue();
        ArticleMatch.FromCandidates([]).IsMissing.Should().BeTrue();
        ArticleMatch.NotFound("blocked").IsMissing.Should().BeFalse();
        ArticleMatch.Found(new BexioArticle()).IsMissing.Should().BeFalse();
    }
}
