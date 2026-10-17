using BexioOrderImport.Abstractions.Bexio;

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

    public async Task<ArticleMatch> FindArticleAsync(EanArticleCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return ArticleMatch.FromCandidates(await _bexio.FindArticlesByInternCodeAsync(criteria.Ean));
    }
}
