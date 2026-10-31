using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio;

/// <summary>
/// A position of a Bexio document as listed in the document, eg. an article or discount position.
/// </summary>
public class BexioPosition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Position type, eg. "KbPositionArticle"; its snake case name is the path of the position endpoints.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
