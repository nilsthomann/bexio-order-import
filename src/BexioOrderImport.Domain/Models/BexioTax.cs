using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models;

public class BexioTax
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("percentage")]
    public decimal Percentage { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}