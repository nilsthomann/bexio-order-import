using System.Globalization;
using System.Text.Json;
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

        /// <summary>
        /// Sale price of the article, null if Bexio has none.
        /// </summary>
        [JsonPropertyName("sale_price")]
        [JsonConverter(typeof(DecimalOrStringJsonConverter))]
        public decimal? SalePrice { get; set; }
    }

    /// <summary>
    /// Reads a decimal that Bexio may send as a number or as a string (eg. "12.50").
    /// </summary>
    public class DecimalOrStringJsonConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetDecimal(),
                JsonTokenType.String when decimal.TryParse(reader.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) => value,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteNumberValue(value.Value);
            else writer.WriteNullValue();
        }
    }
}
