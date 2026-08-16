using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

public class BexioCreateContactRequest
{
    [JsonPropertyName("contact_type_id")]
    public int ContactTypeId { get; set; } = 1; // 1 = Company, 2 = Person

    [JsonPropertyName("name_1")]
    public string Name1 { get; set; } = string.Empty;

    [JsonPropertyName("mail")]
    public string Mail { get; set; } = string.Empty;

    [JsonPropertyName("street_name")]
    public string StreetName { get; set; } = string.Empty;

    [JsonPropertyName("postcode")]
    public string Postcode { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public int UserId { get; set; } = 1;

    [JsonPropertyName("owner_id")]
    public int OwnerId { get; set; } = 1;
}
