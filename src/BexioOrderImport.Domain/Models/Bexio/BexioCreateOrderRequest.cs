using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

public class BexioCreateOrderRequest
{
    [JsonPropertyName("contact_id")]
    public int ContactId { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; } = 1;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("mwst_type")]
    public int MwstType { get; set; } = 0; // 0 = Excl VAT, 1 = Incl VAT

    [JsonPropertyName("currency_id")]
    public int CurrencyId { get; set; } = 1; // 1 = CHF

    [JsonPropertyName("payment_type_id")]
    public int PaymentTypeId { get; set; } = 1;

    [JsonPropertyName("language_id")]
    public int LanguageId { get; set; } = 1;

    [JsonPropertyName("api_reference")]
    public string? ApiReference { get; set; } = "Excel-Import";

    [JsonPropertyName("positions")]
    public List<object> Positions { get; set; } = [];
}
