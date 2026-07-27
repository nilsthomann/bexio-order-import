using System.Collections.Generic;
using System.Text.Json.Serialization;
using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Wpf.Models;

public class AppSettingsDto
{
    [JsonPropertyName("Bexio")]
    public BexioSettingsDto Bexio { get; set; } = new();

    [JsonPropertyName("ActiveProfileName")]
    public string ActiveProfileName { get; set; } = "Default";

    [JsonPropertyName("Profiles")]
    public List<MappingProfileDto> Profiles { get; set; } = new();
}

public class BexioSettingsDto
{
    [JsonPropertyName("ApiToken")]
    public string ApiToken { get; set; } = "bexio_api_token_here";

    [JsonPropertyName("AccountId")]
    public int? AccountId { get; set; } = null;

    [JsonPropertyName("TaxId")]
    public int? TaxId { get; set; } = null;

    [JsonPropertyName("Language")]
    public string Language { get; set; } = "de";
}

public class MappingProfileDto
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("ExcelMapping")]
    public ExcelMappingOptions ExcelMapping { get; set; } = new();
}
