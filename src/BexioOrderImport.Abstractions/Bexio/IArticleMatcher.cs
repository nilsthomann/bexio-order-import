using System.Diagnostics.CodeAnalysis;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Abstractions.Bexio;

/// <summary>
/// Finds the Bexio article of an order position. Each format defines its own criteria record, so a new criterion is a new
/// property and a fallback is another matcher of the same criteria that runs when the first one finds nothing.
/// </summary>
public interface IArticleMatcher<in TCriteria>
{
    Task<ArticleMatch> FindArticleAsync(TCriteria criteria);
}

/// <summary>
/// Result of an article lookup: the matched article, or the reason why none matched (eg. "not found").
/// </summary>
public sealed record ArticleMatch(BexioArticle? Article, string? FailureReason = null)
{
    public const string NotFoundReason = "not found";

    [MemberNotNullWhen(true, nameof(Article))]
    public bool IsFound => Article != null;

    /// <summary>
    /// No article was found at all, so a fallback matcher may try other criteria. Other failures (eg. "not unique") are final.
    /// </summary>
    public bool IsMissing => !IsFound && FailureReason == NotFoundReason;

    public static ArticleMatch Found(BexioArticle article) => new(article);

    public static ArticleMatch NotFound(string reason = NotFoundReason) => new(null, reason);

    /// <summary>
    /// Matches if exactly one candidate is given, otherwise the reason is "not found" or "not unique (n articles)".
    /// </summary>
    public static ArticleMatch FromCandidates(IReadOnlyList<BexioArticle> candidates) => candidates.Count switch
    {
        1 => Found(candidates[0]),
        0 => NotFound(),
        _ => NotFound($"not unique ({candidates.Count} articles)")
    };
}
