using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

public class BexioCreateDiscountPositionRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "Rabatt";

    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("is_percentual")]
    public bool IsPercentual { get; set; } = true;
}
