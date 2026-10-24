using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

public class BexioPaymentType
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
