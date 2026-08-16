using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioContact
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("mail")]
        public string? EMail { get; set; }
    }
}
