using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models;

public class BexioAccount
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("account_no")]
    public string AccountNo { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("account_type")]
    public int? AccountType { get; set; }
}