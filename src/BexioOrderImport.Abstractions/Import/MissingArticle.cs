namespace BexioOrderImport.Abstractions.Import;

/// <summary>
/// Order position without a Bexio article. The host lists these, so the user can skip them and add them to Bexio by hand;
/// a value that is empty in every position gets no column.
/// </summary>
public sealed record MissingArticle
{
    /// <summary>
    /// Excel row of the position, 0 if unknown.
    /// </summary>
    public int Row { get; init; }
    public string? Ean { get; init; }
    public string? ArticleNumber { get; init; }
    public string? Color { get; init; }
    public string? SeasonCode { get; init; }
    public string? Size { get; init; }

    /// <summary>
    /// Pieces to add in Bexio.
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// <see cref="Bexio.ArticleMatch.CandidateCount"/>: more than one if the article is not unique.
    /// </summary>
    public int CandidateCount { get; init; }
}
