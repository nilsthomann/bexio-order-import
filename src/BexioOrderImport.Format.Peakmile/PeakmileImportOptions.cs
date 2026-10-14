namespace BexioOrderImport.Format.Peakmile;

public record PeakmileImportOptions(
    string DefaultOrderName = "Order: {CustomerName} {SeasonCode}",
    string SeasonCode = "FS27",
    string PositionTextTemplate = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}",
    string DiscountPositionTextTemplate = "Rabatt ({DiscountInPercent}%)",
    string SizeRowTemplate = "{Amount}x Size {Size}",
    IReadOnlyList<string>? SeasonFallbackCodes = null
);
