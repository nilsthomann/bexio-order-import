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
}
