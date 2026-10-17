namespace BexioOrderImport.Abstractions.Bexio;

/// <summary>
/// Runs the matchers in order: the next one runs only if the previous found no article at all.
/// A final failure such as "not unique" stops the lookup, so a fallback never hides an ambiguous match.
/// </summary>
public sealed class FallbackArticleMatcher<TCriteria> : IArticleMatcher<TCriteria>
{
    private readonly IReadOnlyList<IArticleMatcher<TCriteria>> _matchers;

    public FallbackArticleMatcher(params IArticleMatcher<TCriteria>[] matchers)
    {
        ArgumentNullException.ThrowIfNull(matchers);
        if (matchers.Length == 0)
        {
            throw new ArgumentException("At least one matcher is required.", nameof(matchers));
        }
        _matchers = matchers;
    }

    public async Task<ArticleMatch> FindArticleAsync(TCriteria criteria)
    {
        ArticleMatch match = ArticleMatch.NotFound();
        foreach (var matcher in _matchers)
        {
            match = await matcher.FindArticleAsync(criteria);
            if (!match.IsMissing)
            {
                break;
            }
        }
        return match;
    }
}
