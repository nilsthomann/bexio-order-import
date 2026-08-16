using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

public class BexioCreateArticlePositionRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("article_id")]
    public int ArticleId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("account_id")]
    public int? AccountId { get; set; }

    [JsonPropertyName("tax_id")]
    public int? TaxId { get; set; }

    [JsonPropertyName("discount_in_percent")]
    public decimal DiscountInPercent { get; set; }
}
