using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioContact
    {
        private string _email = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("email")]
        public string EMail { get => _email; set => _email = value; }

        [JsonPropertyName("mail")]
        public string Mail { get => _email; set => _email = value; }
    }
}
