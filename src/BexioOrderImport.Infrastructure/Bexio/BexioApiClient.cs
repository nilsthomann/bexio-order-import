using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BexioOrderImport.Infrastructure.Bexio;

public class BexioApiClient : IBexioClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly int? _accountId;
    private readonly int? _taxId;
    private readonly string _language;

    private List<BexioAccount>? _cachedAccounts;
    private List<BexioTax>? _cachedTaxes;
    private List<BexioArticle> _cachedArticles = [];

    public BexioApiClient(HttpClient httpClient, string apiToken, int? accountId, int? taxId, string language = "de")
    {
        _httpClient = httpClient;
        _apiToken = apiToken;
        _accountId = accountId;
        _taxId = taxId;
        _language = language;

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.bexio.com/");
        }
    }

    /// <summary>
    /// Creates an <see cref="HttpRequestMessage"/> with per-request Authorization and Accept
    /// headers. This is thread-safe unlike mutating <c>DefaultRequestHeaders</c>.
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Accept-Language", _language);
        if (content != null) request.Content = content;
        return request;
    }

    public async Task<int?> FindContactIdAsync(string email)
    {
        var searchPayload = new[]
        {
            new { field = "mail", value = email, criteria = "=" }
        };

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/contact/search", body));

        if (response.IsSuccessStatusCode)
        {
            var contacts = await response.Content.ReadFromJsonAsync<List<BexioContact>>();
            if (contacts != null && contacts.Count > 0)
            {
                return contacts[0].Id;
            }
        }

        return null;
    }

    public async Task<int> CreateContactAsync(Customer customer)
    {
        var createPayload = new
        {
            contact_type_id = 1, // 1 = Company, 2 = Person
            name_1 = customer.CompanyName,
            mail = customer.Email,
            street_name = customer.Street,
            postcode = customer.ZipCode,
            city = customer.City,
            user_id = 1, // Default userid
            owner_id = 1 // Default owner
        };

        var body = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
        var createResponse = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/contact", body));
        createResponse.EnsureSuccessStatusCode();

        var newContact = await createResponse.Content.ReadFromJsonAsync<BexioContact>();
        return newContact?.Id ?? throw new Exception("Error creating contact in Bexio.");
    }

    public async Task<int> CreateOrderAsync(int contactId, Order order)
    {
        var orderPayload = new
        {
            contact_id = contactId,
            user_id = 1, // Default user
            title = string.IsNullOrEmpty(order.Title) ? $"Order: {order.Customer.CompanyName}" : order.Title,
            mwst_type = 0, // 0 = excl. VAT, 1 = incl. VAT
            currency_id = 1, // 1 = CHF
            payment_type_id = 1, // Default
            language_id = 1, // German
            api_reference = "Excel-Import",
            positions = Array.Empty<object>() // Positions added subsequently
        };

        var body = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/kb_order", body));
        response.EnsureSuccessStatusCode();

        var createdOrder = await response.Content.ReadFromJsonAsync<BexioOrder>();
        return createdOrder?.Id ?? throw new Exception("Error creating order in Bexio.");
    }

    public async Task<string?> GetOrderContactEmailAsync(int orderId)
    {
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, $"2.0/kb_order/{orderId}"));
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var order = await response.Content.ReadFromJsonAsync<BexioOrder>();
        if (order == null) return null;

        var contactResponse = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, $"2.0/contact/{order.ContactId}"));
        if (!contactResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var contact = await contactResponse.Content.ReadFromJsonAsync<BexioContact>();
        return contact?.EMail;
    }

    public async Task<BexioArticle?> FindArticleAsync(string articleNumber, string color, string seasonCode)
    {
        // Color can be "Black" or eg. "140 Black", we need only the Color Name
        string cleanColor = Regex.Replace(color, @"^\d+", "").Trim();

        var articles = await FindArticlesByInternCodeAsync(articleNumber, cleanColor, seasonCode);

        if (articles?.Count == 1)
        {
            return articles[0];
        }

        // Fallback 
        return await FindArticleByArticleNumberAndFilterAsync(articleNumber, cleanColor, seasonCode);
    }

    public async Task PreFetchArticlesAsync(string seasonCode, IEnumerable<string> articleNumbers)
    {
        var distinctNumbers = articleNumbers
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct()
            .ToList();

        if (distinctNumbers.Count == 0) return;

        List<BexioArticle>? fetchedArticles = null;

        if (!string.IsNullOrWhiteSpace(seasonCode))
        {
            var searchPayload = new[]
            {
                new { field = "intern_code", value = seasonCode.Trim(), criteria = "like" }
            };

            var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
            var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/article/search", body));

            if (response.IsSuccessStatusCode)
            {
                fetchedArticles = await response.Content.ReadFromJsonAsync<List<BexioArticle>>();
            }
        }

        if (fetchedArticles == null || fetchedArticles.Count == 0)
        {
            foreach (var artNo in distinctNumbers)
            {
                if (_cachedArticles.Any(a => a.Code.Contains(artNo, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var searchPayload = new[]
                {
                    new { field = "intern_code", value = artNo, criteria = "like" }
                };

                var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
                var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/article/search", body));

                if (response.IsSuccessStatusCode)
                {
                    var articles = await response.Content.ReadFromJsonAsync<List<BexioArticle>>();
                    if (articles != null && articles.Count > 0)
                    {
                        var relevant = articles.Where(a => distinctNumbers.Any(num => a.Code.Contains(num, StringComparison.OrdinalIgnoreCase)));
                        _cachedArticles = [.. _cachedArticles.UnionBy(relevant, x => x.Id)];
                    }
                }
            }
            return;
        }

        // Save only articles in _cachedArticles that are present in the provided article numbers list
        var matchingArticles = fetchedArticles.Where(a =>
            distinctNumbers.Any(num => a.Code.Contains(num, StringComparison.OrdinalIgnoreCase)));

        _cachedArticles = [.. _cachedArticles.UnionBy(matchingArticles, x => x.Id)];
    }

    private async Task<List<BexioArticle>?> FindArticlesByInternCodeAsync(string articleNumber, string cleanColor, string seasonCode)
    {
        string internCode = $"{seasonCode?.Trim() ?? string.Empty}{articleNumber.Trim()}{cleanColor}".Trim();

        var searchPayload = new[]
        {
            new { field = "intern_code", value = internCode, criteria = "=" }
        };

        if (_cachedArticles.Any(x => x.Code.Equals(internCode, StringComparison.OrdinalIgnoreCase)))
            return [.. _cachedArticles.Where(x => x.Code.Equals(internCode, StringComparison.OrdinalIgnoreCase))];

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/article/search", body));

        if (response.IsSuccessStatusCode)
        {
            var articles = await response.Content.ReadFromJsonAsync<List<BexioArticle>>();
            if (articles != null && articles.Count > 0)
            {
                _cachedArticles = [.. _cachedArticles.UnionBy(articles, x => x.Id)];
                return articles;
            }
        }

        return null;
    }

    private async Task<BexioArticle?> FindArticleByArticleNumberAndFilterAsync(string articleNumber, string cleanColor, string seasonCode)
    {
        var searchPayload = new[]
        {
            new { field = "intern_code", value = articleNumber.Trim(), criteria = "like" }
        };

        var cachedArticle = _cachedArticles.FirstOrDefault(x =>
            x.Code.Contains(articleNumber.Trim(), StringComparison.OrdinalIgnoreCase) &&
            x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) &&
            x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
        if (cachedArticle != null) return cachedArticle;

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/article/search", body));

        if (response.IsSuccessStatusCode)
        {
            var articles = await response.Content.ReadFromJsonAsync<List<BexioArticle>>();
            if (articles != null)
            {
                _cachedArticles = [.. _cachedArticles.UnionBy(articles, x => x.Id)];

                if (articles.Count == 1)
                {
                    return articles[0];
                }
                else if (articles.Count > 1)
                {
                    return articles.FirstOrDefault(x =>
                        x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) &&
                        x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        return null;
    }

    public async Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position)
    {
        var text = string.IsNullOrEmpty(position.PositionText)
            ? $"Color: {position.Color}, Size: {position.Size}"
            : position.PositionText;

        var positionPayload = new
        {
            amount = position.Quantity,
            article_id = articleId,
            text,
            unit_price = position.UnitPrice,
            account_id = _accountId,
            tax_id = _taxId,
            discount_in_percent = position.DiscountPercent
        };

        var body = new StringContent(JsonSerializer.Serialize(positionPayload), Encoding.UTF8, "application/json");
        var request = CreateRequest(HttpMethod.Post, $"2.0/kb_order/{orderId}/kb_position_article", body);
        var response = await SendWithRateLimitCheckAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error adding position: {errorContent}");
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, "2.0/contact?limit=1"));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<BexioAccount>> GetAccountsAsync()
    {
        if (_cachedAccounts != null) return _cachedAccounts;
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, "2.0/accounts"));
        response.EnsureSuccessStatusCode();
        var accounts = await response.Content.ReadFromJsonAsync<List<BexioAccount>>();
        _cachedAccounts = accounts?.Where(a => a.IsActive && a.AccountType == 1).ToList() ?? [];
        return _cachedAccounts;
    }

    public async Task<List<BexioTax>> GetTaxesAsync()
    {
        if (_cachedTaxes != null) return _cachedTaxes;
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, "3.0/taxes"));
        response.EnsureSuccessStatusCode();
        var taxes = await response.Content.ReadFromJsonAsync<List<BexioTax>>();
        _cachedTaxes = taxes?.Where(t => t.IsActive && (t.Type == "sales_tax" || t.Type == "not_taxable_turnover")).ToList() ?? [];
        return _cachedTaxes;
    }

    /// <summary>
    /// Sends an HTTP request and inspects Bexio Rate Limit headers.
    /// If remaining requests hit 0 or HTTP 429 is returned, automatically pauses execution until the reset window.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRateLimitCheckAsync(HttpRequestMessage request)
    {
        var response = await _httpClient.SendAsync(request);

        ProcessRateLimitHeaders(response.Headers, out int remaining, out int resetSeconds);

        if (response.StatusCode == (System.Net.HttpStatusCode)429)
        {
            int delaySeconds = Math.Max(resetSeconds, 1);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

            var clonedRequest = CloneRequest(request);
            return await SendWithRateLimitCheckAsync(clonedRequest);
        }
        else if (remaining <= 0 && resetSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(resetSeconds));
        }

        return response;
    }

    private static void ProcessRateLimitHeaders(HttpResponseHeaders headers, out int remaining, out int resetSeconds)
    {
        remaining = int.MaxValue;
        resetSeconds = 60;

        if (TryGetHeaderValue(headers, "ratelimit-remaining", out var remStr))
        {
            if (int.TryParse(remStr, out int remVal))
            {
                remaining = remVal;
            }
        }

        if (TryGetHeaderValue(headers, "ratelimit-reset", out var resetStr))
        {
            if (int.TryParse(resetStr, out int resetVal))
            {
                if (resetVal > 1_000_000_000) // Unix timestamp in seconds
                {
                    long currentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    resetSeconds = Math.Max(1, (int)(resetVal - currentUnix));
                }
                else
                {
                    resetSeconds = Math.Max(1, resetVal);
                }
            }
        }
    }

    private static bool TryGetHeaderValue(HttpResponseHeaders headers, string headerName, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (headers.TryGetValues(headerName, out var values))
        {
            value = values.FirstOrDefault();
            return !string.IsNullOrEmpty(value);
        }
        return false;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (request.Content != null)
        {
            var ms = new MemoryStream();
            request.Content.CopyToAsync(ms).GetAwaiter().GetResult();
            ms.Position = 0;
            var contentClone = new StreamContent(ms);
            foreach (var header in request.Content.Headers)
            {
                contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = contentClone;
        }
        return clone;
    }
}
