using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioArticle
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("intern_code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("intern_name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("intern_description")]
        public string Description { get; set; } = string.Empty;
    }
}
