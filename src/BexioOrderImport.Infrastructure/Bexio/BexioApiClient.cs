using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

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
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Post, "2.0/contact/search", body));

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
        var createResponse = await _httpClient.SendAsync(CreateRequest(HttpMethod.Post, "2.0/contact", body));
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
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Post, "2.0/kb_order", body));
        response.EnsureSuccessStatusCode();

        var createdOrder = await response.Content.ReadFromJsonAsync<BexioOrder>();
        return createdOrder?.Id ?? throw new Exception("Error creating order in Bexio.");
    }

    public async Task<string?> GetOrderContactEmailAsync(int orderId)
    {
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, $"2.0/kb_order/{orderId}"));
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var order = await response.Content.ReadFromJsonAsync<BexioOrder>();
        if (order == null) return null;

        var contactResponse = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, $"2.0/contact/{order.ContactId}"));
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

    private async Task<List<BexioArticle>?> FindArticlesByInternCodeAsync(string articleNumber, string cleanColor, string seasonCode)
    {
        string internCode = $"{seasonCode?.Trim() ?? string.Empty}{articleNumber.Trim()}{cleanColor}".Trim();

        var searchPayload = new[]
        {
            new { field = "intern_code", value = internCode, criteria = "=" }
        };

        if (_cachedArticles.Any(x => x.Code == internCode))
            return [.. _cachedArticles.Where(x => x.Code == internCode)];

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Post, "2.0/article/search", body));

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<BexioArticle>>();
        }

        return null;
    }

    private async Task<BexioArticle?> FindArticleByArticleNumberAndFilterAsync(string articleNumber, string cleanColor, string seasonCode)
    {
        var searchPayload = new[]
        {
            new { field = "intern_code", value = articleNumber.Trim(), criteria = "like" }
        };

        var cachedArticle = _cachedArticles.SingleOrDefault(x => x.Code.Contains(articleNumber.Trim(), StringComparison.InvariantCulture) && x.Name.Contains(cleanColor, StringComparison.InvariantCulture) && x.Name.Contains(seasonCode, StringComparison.InvariantCulture));
        if (cachedArticle != null) return cachedArticle;

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Post, "2.0/article/search", body));

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
                    return articles.SingleOrDefault(x => x.Name.Contains(cleanColor, StringComparison.OrdinalIgnoreCase) && x.Name.Contains(seasonCode, StringComparison.OrdinalIgnoreCase));
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
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Post, $"2.0/kb_order/{orderId}/kb_position_article", body));
        response.EnsureSuccessStatusCode();
    }



    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, "2.0/contact?limit=1"));
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
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, "2.0/accounts"));
        response.EnsureSuccessStatusCode();
        var accounts = await response.Content.ReadFromJsonAsync<List<BexioAccount>>();
        _cachedAccounts = accounts?.Where(a => a.IsActive && a.AccountType == 1).ToList() ?? [];
        return _cachedAccounts;
    }

    public async Task<List<BexioTax>> GetTaxesAsync()
    {
        if (_cachedTaxes != null) return _cachedTaxes;
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, "3.0/taxes"));
        response.EnsureSuccessStatusCode();
        var taxes = await response.Content.ReadFromJsonAsync<List<BexioTax>>();
        _cachedTaxes = taxes?.Where(t => t.IsActive && (t.Type == "sales_tax" || t.Type == "not_taxable_turnover")).ToList() ?? [];
        return _cachedTaxes;
    }

}
