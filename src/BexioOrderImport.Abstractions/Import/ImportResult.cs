namespace BexioOrderImport.Abstractions.Import;

/// <param name="MissingArticles">Positions the user skipped because their article is missing in Bexio; null if none.</param>
public record ImportResult(
    bool Success,
    string? OrderNumber = null,
    int UploadedPositionsCount = 0,
    string? ErrorMessage = null,
    IReadOnlyList<MissingArticle>? MissingArticles = null
);
