using System.Text.Json.Serialization;

namespace BexioOrderImport.Domain.Models.Bexio
{
    public class BexioArticle
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("intern_name")]
        public string InternName { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class DuplicateArticleException : System.Exception
    {
        public string SearchQuery { get; }
        public int MatchCount { get; }

        public DuplicateArticleException(string searchQuery, int matchCount)
            : base($"Multiple articles ({matchCount} found) matched the search query '{searchQuery}'.")
        {
            SearchQuery = searchQuery;
            MatchCount = matchCount;
        }
    }
}
