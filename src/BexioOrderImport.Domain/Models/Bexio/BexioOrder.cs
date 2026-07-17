using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioOrder
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("document_nr")]
        public string DocumentNr { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public int UserId { get; set; } = 1; // Default user

        [JsonPropertyName("mwst_type")]  
        public MwstType MwstType { get; set; }

        [JsonPropertyName("currency_id")]
        public int CurrencyId { get; set; }

        [JsonPropertyName("payment_type_id")]
        public int PaymentTypeId { get; set; }

        [JsonPropertyName("language_id")]
        public int LanguageId { get; set; }

        [JsonPropertyName("api_reference")]
        public string? ApiReference { get; set; }

        [JsonPropertyName("contact_id")]
        public int ContactId { get; set; }
        
    }

    public enum MwstType
    {
        ExclMwst = 0,
        InclMwst = 1
    }
}
