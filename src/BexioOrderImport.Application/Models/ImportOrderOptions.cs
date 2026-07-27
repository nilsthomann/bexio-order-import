namespace BexioOrderImport.Application.Models;

public record ImportOrderOptions(
    string DefaultOrderName = "Order: {CustomerName} {SeasonCode}",
    string SeasonCode = "FS27",
    string PositionTextTemplate = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}",
    string DiscountPositionTextTemplate = "Rabatt ({DiscountInPercent}%)"
);
