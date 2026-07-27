using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioArticle
    {
        private string _name = string.Empty;
        private string _description = string.Empty;

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("intern_code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("intern_name")]
        public string Name { get => _name; set => _name = value; }

        [JsonPropertyName("title")]
        public string Title { get => _name; set => _name = value; }

        [JsonPropertyName("internal_name")]
        public string InternalName { get => _name; set => _name = value; }

        [JsonPropertyName("intern_description")]
        public string Description { get => _description; set => _description = value; }

        [JsonPropertyName("description")]
        public string StandardDescription { get => _description; set => _description = value; }

        [JsonPropertyName("internal_description")]
        public string InternalDescription { get => _description; set => _description = value; }
    }
}
