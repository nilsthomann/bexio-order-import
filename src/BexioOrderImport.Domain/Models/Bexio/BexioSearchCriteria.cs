using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

public class BexioSearchCriteria
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("criteria")]
    public string Criteria { get; set; } = "=";
}
