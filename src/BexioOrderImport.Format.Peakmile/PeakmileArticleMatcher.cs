using System.Text.RegularExpressions;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Format.Peakmile;

/// <summary>
/// Criteria to find the Bexio article of a Peakmile position. The fallback season codes are tried in order
/// (exact intern code match only) when the season code yields no match.
/// </summary>
public sealed record PeakmileArticleCriteria(string ArticleNumber, string Color, string SeasonCode, IReadOnlyList<string>? FallbackSeasonCodes = null);

/// <summary>
/// Finds the Bexio article of a Peakmile position.
/// </summary>
public interface IPeakmileArticleMatcher : IArticleMatcher<PeakmileArticleCriteria>
{
    /// <summary>
    /// Pre-fetches the articles of the season code and keeps those of the given article numbers, to speed up the lookups of an import.
    /// </summary>
    Task PreFetchArticlesAsync(string seasonCode, IEnumerable<string> articleNumbers);
}

/// <summary>
/// Matches the Bexio intern_code exactly as {SeasonCode}{ArticleNumber}{Color}, first with the season code, then with each
/// fallback code. Only if all miss, it searches by article number and filters by color and season code in the article name.
/// Found articles are cached for the lifetime of the matcher (one import).
/// </summary>
public sealed class PeakmileArticleMatcher : IPeakmileArticleMatcher
{
    private readonly IBexioService _bexio;
    private List<BexioArticle> _cachedArticles = [];

    public PeakmileArticleMatcher(IBexioService bexio)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
    }

    public async Task<ArticleMatch> FindArticleAsync(PeakmileArticleCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // Color can be "Black" or eg. "140 Black", we need only the Color Name
        string cleanColor = Regex.Replace(criteria.Color, @"^\d+", "").Trim();

        // Season code first, then the fallback codes of multi-season articles (eg. "NOOS", "EX")
        // ponytail: a fallback article costs one missed season code lookup per position; cache missed intern codes if that gets slow
        foreach (var code in (string[])[criteria.SeasonCode, .. criteria.FallbackSeasonCodes ?? []])
        {
            var articles = await FindArticlesByInternCodeAsync(criteria.ArticleNumber, cleanColor, code);

            if (articles.Count == 1)
            {
                return ArticleMatch.Found(articles[0]);
            }
        }

        // Fallback
        var article = await FindArticleByArticleNumberAndFilterAsync(criteria.ArticleNumber, cleanColor, criteria.SeasonCode);
        return article != null ? ArticleMatch.Found(article) : ArticleMatch.NotFound();
    }

    public async Task PreFetchArticlesAsync(string seasonCode, IEnumerable<string> articleNumbers)
    {
        var distinctNumbers = articleNumbers
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct()
            .ToList();

        if (distinctNumbers.Count == 0) return;

        IReadOnlyList<BexioArticle> fetchedArticles = [];

        if (!string.IsNullOrWhiteSpace(seasonCode))
        {
            fetchedArticles = await SearchByInternCodeLikeAsync(seasonCode.Trim());
        }

        if (fetchedArticles.Count == 0)
        {
            foreach (var artNo in distinctNumbers)
            {
                if (_cachedArticles.Any(a => a.Code.Contains(artNo, StringComparison.OrdinalIgnoreCase))) continue;

                var articles = await SearchByInternCodeLikeAsync(artNo);
                var relevant = articles.Where(a => distinctNumbers.Any(num => a.Code.Contains(num, StringComparison.OrdinalIgnoreCase)));
                AddToCache(relevant);
            }
            return;
        }

        // Save only articles in _cachedArticles that are present in the provided article numbers list
        AddToCache(fetchedArticles.Where(a => distinctNumbers.Any(num => a.Code.Contains(num, StringComparison.OrdinalIgnoreCase))));
    }

    private async Task<IReadOnlyList<BexioArticle>> FindArticlesByInternCodeAsync(string articleNumber, string cleanColor, string seasonCode)
    {
        string internCode = $"{seasonCode?.Trim() ?? string.Empty}{articleNumber.Trim()}{cleanColor}".Trim();

        var cached = _cachedArticles.Where(x => x.Code.Equals(internCode, StringComparison.OrdinalIgnoreCase)).ToList();
        if (cached.Count > 0) return cached;

        var articles = await _bexio.FindArticlesByInternCodeAsync(internCode);
        AddToCache(articles);
        return articles;
    }

    private async Task<BexioArticle?> FindArticleByArticleNumberAndFilterAsync(string articleNumber, string cleanColor, string seasonCode)
    {
        var cachedArticle = _cachedArticles.FirstOrDefault(x =>
            x.Code.Contains(articleNumber.Trim(), StringComparison.OrdinalIgnoreCase) &&
            x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) &&
            x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
        if (cachedArticle != null) return cachedArticle;

        var articles = await SearchByInternCodeLikeAsync(articleNumber.Trim());
        AddToCache(articles);

        if (articles.Count == 1)
        {
            return articles[0];
        }
        if (articles.Count > 1)
        {
            return articles.FirstOrDefault(x =>
                x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) &&
                x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private Task<IReadOnlyList<BexioArticle>> SearchByInternCodeLikeAsync(string value)
        => _bexio.SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = value, Criteria = "like" }]);

    private void AddToCache(IEnumerable<BexioArticle> articles)
    {
        _cachedArticles = [.. _cachedArticles.UnionBy(articles, x => x.Id)];
    }
}
