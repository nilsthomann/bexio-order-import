using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// Criteria to find the Bexio article of an EAN list position.
/// </summary>
public sealed record EanArticleCriteria(string Ean);

/// <summary>
/// Matches the EAN exactly against the Bexio intern_code; exactly one article must match.
/// </summary>
public sealed class EanArticleMatcher : IArticleMatcher<EanArticleCriteria>
{
    private readonly IBexioService _bexio;

    public EanArticleMatcher(IBexioService bexio)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
    }

    /// <summary>
    /// The exact lookup, then <see cref="LeadingZeroEanArticleMatcher"/> if no article has the EAN as it is written.
    /// </summary>
    public static IArticleMatcher<EanArticleCriteria> WithFallbacks(IBexioService bexio)
        => new FallbackArticleMatcher<EanArticleCriteria>(new EanArticleMatcher(bexio), new LeadingZeroEanArticleMatcher(bexio));

    public async Task<ArticleMatch> FindArticleAsync(EanArticleCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return ArticleMatch.FromCandidates(await _bexio.FindArticlesByInternCodeAsync(criteria.Ean));
    }
}

/// <summary>
/// Matches the EAN against the Bexio intern_code ignoring leading zeros, eg. the UPC 826710896036 (Excel drops the zero of
/// number cells) and the EAN-13 0826710896036. It searches the intern codes that contain the EAN without leading zeros and
/// keeps only the codes that are equal apart from leading zeros; exactly one article must match.
/// </summary>
public sealed class LeadingZeroEanArticleMatcher : IArticleMatcher<EanArticleCriteria>
{
    private readonly IBexioService _bexio;

    public LeadingZeroEanArticleMatcher(IBexioService bexio)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
    }

    public async Task<ArticleMatch> FindArticleAsync(EanArticleCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        string significant = WithoutLeadingZeros(criteria.Ean);
        if (significant.Length == 0)
        {
            return ArticleMatch.NotFound();
        }

        var articles = await _bexio.SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = significant, Criteria = "like" }]);
        return ArticleMatch.FromCandidates([.. articles.Where(a => WithoutLeadingZeros(a.Code) == significant)]);
    }

    private static string WithoutLeadingZeros(string? code) => (code ?? string.Empty).Trim().TrimStart('0');
}
