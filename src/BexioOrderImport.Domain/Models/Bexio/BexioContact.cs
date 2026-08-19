using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioContact
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nr")]
        public string? Nr { get; set; }

        [JsonPropertyName("mail")]
        public string? EMail { get; set; }

        [JsonPropertyName("name_1")]
        public string Name { get; set; } = string.Empty;
    }
}
