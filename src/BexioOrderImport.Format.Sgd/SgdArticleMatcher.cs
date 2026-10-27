using System.Text.RegularExpressions;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Criteria to find the Bexio article of an SGD position. The fallback season codes are tried in order
/// (exact intern code match only) when the season code yields no match.
/// </summary>
public sealed record SgdArticleCriteria(string ArticleNumber, string Color, string SeasonCode, IReadOnlyList<string>? FallbackSeasonCodes = null);

/// <summary>
/// Finds the Bexio article of an SGD position.
/// </summary>
public interface ISgdArticleMatcher : IArticleMatcher<SgdArticleCriteria>
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
public sealed class SgdArticleMatcher : ISgdArticleMatcher
{
    private readonly IBexioService _bexio;
    private List<BexioArticle> _cachedArticles = [];

    public SgdArticleMatcher(IBexioService bexio)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
    }

    public async Task<ArticleMatch> FindArticleAsync(SgdArticleCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // The color can have a color code in front, eg. "1001 Black"; the intern code has only the color name
        string cleanColor = Regex.Replace(criteria.Color, @"^\d+", "").Trim();

        string[] seasonCodes = [criteria.SeasonCode, .. criteria.FallbackSeasonCodes ?? []];
        foreach (var code in seasonCodes)
        {
            var articles = await FindArticlesByInternCodeAsync(criteria.ArticleNumber, cleanColor, code);
            if (articles.Count == 1)
            {
                return ArticleMatch.Found(articles[0]);
            }
        }

        var article = await FindArticleByArticleNumberAndFilterAsync(criteria.ArticleNumber, cleanColor, seasonCodes);
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
            foreach (var articleNumber in distinctNumbers)
            {
                if (_cachedArticles.Any(a => a.Code.Contains(articleNumber, StringComparison.OrdinalIgnoreCase))) continue;

                var articles = await SearchByInternCodeLikeAsync(articleNumber);
                AddToCache(articles.Where(a => distinctNumbers.Any(n => a.Code.Contains(n, StringComparison.OrdinalIgnoreCase))));
            }
            return;
        }

        // Keep only the articles of this import
        AddToCache(fetchedArticles.Where(a => distinctNumbers.Any(n => a.Code.Contains(n, StringComparison.OrdinalIgnoreCase))));
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

    private async Task<BexioArticle?> FindArticleByArticleNumberAndFilterAsync(string articleNumber, string cleanColor, string[] seasonCodes)
    {
        string seasonCode = seasonCodes[0];
        var cachedArticle = FindByInternCodeIgnoringSpaces(_cachedArticles, articleNumber, cleanColor, seasonCodes)
            ?? _cachedArticles.FirstOrDefault(x =>
                x.Code.Contains(articleNumber.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) &&
                x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
        if (cachedArticle != null) return cachedArticle;

        var articles = await SearchByInternCodeLikeAsync(articleNumber.Trim());
        AddToCache(articles);

        var spacedArticle = FindByInternCodeIgnoringSpaces(articles, articleNumber, cleanColor, seasonCodes);
        if (spacedArticle != null) return spacedArticle;

        if (articles.Count == 1)
        {
            return articles[0];
        }
        return articles.FirstOrDefault(x =>
            x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) &&
            x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
    }

    // Some intern codes are stored with spaces (eg. "NOOS 5440 Black"), which the exact Bexio search misses
    private static BexioArticle? FindByInternCodeIgnoringSpaces(IEnumerable<BexioArticle> articles, string articleNumber, string cleanColor, string[] seasonCodes)
    {
        foreach (var code in seasonCodes)
        {
            string internCode = RemoveWhitespace($"{code}{articleNumber}{cleanColor}");
            var article = articles.FirstOrDefault(a => RemoveWhitespace(a.Code).Equals(internCode, StringComparison.OrdinalIgnoreCase));
            if (article != null) return article;
        }
        return null;
    }

    private static string RemoveWhitespace(string value) => Regex.Replace(value, @"\s+", "");

    private Task<IReadOnlyList<BexioArticle>> SearchByInternCodeLikeAsync(string value)
        => _bexio.SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = value, Criteria = "like" }]);

    private void AddToCache(IEnumerable<BexioArticle> articles)
    {
        _cachedArticles = [.. _cachedArticles.UnionBy(articles, x => x.Id)];
    }
}
