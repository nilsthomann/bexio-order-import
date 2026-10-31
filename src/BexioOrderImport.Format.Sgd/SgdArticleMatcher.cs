using System.Text.RegularExpressions;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Criteria to find the Bexio article of a SGD position. The fallback season codes are tried in order
/// when the season code yields no match.
/// </summary>
public sealed record SgdArticleCriteria(string ArticleNumber, string Color, string SeasonCode, IReadOnlyList<string>? FallbackSeasonCodes = null);

/// <summary>
/// Finds the Bexio article of a SGD position.
/// </summary>
public interface ISgdArticleMatcher : IArticleMatcher<SgdArticleCriteria>
{
    /// <summary>
    /// Loads the articles of the season code once, so the lookups of an import need no request per position.
    /// Does nothing without article numbers.
    /// </summary>
    Task PreFetchArticlesAsync(string seasonCode, IEnumerable<string> articleNumbers);
}

/// <summary>
/// Matches the Bexio intern_code as {SeasonCode}{ArticleNumber}{Color}, ignoring whitespace and case, with the color as written
/// in the order (eg. "1001 Black"), only its name ("Black") or only its code ("1001"); first with the season code, then with each fallback code.
/// So "FS26 E183801 1001 Black", "FS26E1838011001Black", "FS26E183801 Black" and "FS26E1838011001" all match. The articles come from one search per
/// season code (<see cref="PreFetchArticlesAsync"/>) and one per article number that is not found in them; each search runs once
/// per matcher (one import). Several articles with the same code are not unique and match none.
/// </summary>
public sealed partial class SgdArticleMatcher : ISgdArticleMatcher
{
    private readonly IBexioService _bexio;
    private readonly Dictionary<string, List<BexioArticle>> _articlesByCode = [];
    private readonly HashSet<int> _articleIds = [];
    private readonly HashSet<string> _searchedValues = [];

    public SgdArticleMatcher(IBexioService bexio)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
    }

    public async Task<ArticleMatch> FindArticleAsync(SgdArticleCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        string[] codes = CandidateCodes(criteria);
        var match = Match(codes);
        if (match == null)
        {
            await SearchOnceAsync(criteria.ArticleNumber);
            match = Match(codes);
        }
        return match ?? ArticleMatch.NotFound();
    }

    public async Task PreFetchArticlesAsync(string seasonCode, IEnumerable<string> articleNumbers)
    {
        ArgumentNullException.ThrowIfNull(articleNumbers);
        if (articleNumbers.Any(n => !string.IsNullOrWhiteSpace(n)))
        {
            await SearchOnceAsync(seasonCode);
        }
    }

    /// <summary>
    /// Normalized intern codes in the order they are tried: per season code first with the full color, then with its name only, then with its code only.
    /// </summary>
    private static string[] CandidateCodes(SgdArticleCriteria criteria)
    {
        string color = criteria.Color ?? string.Empty;
        var colorCode = ColorCodePrefix().Match(color);
        string colorName = color[colorCode.Length..];
        // A color with code and name also matches by its name or its code alone; a color that is only a code matches as is
        string[] colors = colorCode.Success && colorName.Length > 0 ? [color, colorName, colorCode.Value] : [color];
        return [.. ((string[])[criteria.SeasonCode, .. criteria.FallbackSeasonCodes ?? []])
            .SelectMany(season => colors.Select(c => Normalize($"{season}{criteria.ArticleNumber}{c}")))
            .Distinct()];
    }

    /// <summary>
    /// The articles of the first code that has any, or null if no code has one.
    /// </summary>
    private ArticleMatch? Match(string[] codes)
    {
        foreach (var code in codes)
        {
            if (_articlesByCode.TryGetValue(code, out var articles))
            {
                return ArticleMatch.FromCandidates(articles);
            }
        }
        return null;
    }

    /// <summary>
    /// Loads the articles whose intern code contains the value, once per value.
    /// </summary>
    private async Task SearchOnceAsync(string value)
    {
        string search = value?.Trim() ?? string.Empty;
        if (search.Length == 0 || !_searchedValues.Add(search.ToUpperInvariant())) return;

        var articles = await _bexio.SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = search, Criteria = "like" }]);
        foreach (var article in articles)
        {
            if (!_articleIds.Add(article.Id)) continue;

            string code = Normalize(article.Code);
            if (!_articlesByCode.TryGetValue(code, out var sameCode))
            {
                _articlesByCode[code] = sameCode = [];
            }
            sameCode.Add(article);
        }
    }

    private static string Normalize(string? value) => Whitespace().Replace(value ?? string.Empty, string.Empty).ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // The color can start with a color code, eg. "1001 Black"
    [GeneratedRegex(@"^\s*\d+\s*")]
    private static partial Regex ColorCodePrefix();
}
