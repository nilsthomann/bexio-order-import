using System.Text.Json;
using System.Text.Json.Serialization;

namespace BexioOrderImport.Wpf.Models;

public class AppSettingsDto
{
    [JsonPropertyName("Bexio")]
    public BexioSettingsDto Bexio { get; set; } = new();

    [JsonPropertyName("ActiveProfileName")]
    public string ActiveProfileName { get; set; } = "Default";

    [JsonPropertyName("Profiles")]
    public List<MappingProfileDto> Profiles { get; set; } = [];
}

public class BexioSettingsDto
{
    [JsonPropertyName("ApiToken")]
    public string ApiToken { get; set; } = "bexio_api_token_here";

    [JsonPropertyName("AccountId")]
    public int? AccountId { get; set; } = null;

    [JsonPropertyName("TaxId")]
    public int? TaxId { get; set; } = null;

    /// <summary>
    /// Settings saved before payment types existed lack the field and keep the former fixed payment type 1.
    /// New settings always write the field, also as null until a payment type is selected.
    /// </summary>
    [JsonPropertyName("PaymentTypeId")]
    public int? PaymentTypeId { get; set; } = 1;

    [JsonPropertyName("Language")]
    public string Language { get; set; } = "de";
}

public class MappingProfileDto
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("Format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("Settings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Settings { get; set; }

    /// <summary>
    /// Settings of profiles saved before formats existed (always Peakmile). Only read, new saves use <see cref="Settings"/>.
    /// </summary>
    [JsonPropertyName("ExcelMapping")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ExcelMapping { get; set; }
}
