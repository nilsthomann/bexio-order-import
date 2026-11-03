using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BexioOrderImport.Infrastructure.Bexio;

public class BexioApiClient : IBexioService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly int? _accountId;
    private readonly int? _taxId;
    private readonly int? _paymentTypeId;
    private readonly string _language;

    private readonly Lock _cacheLock = new();
    private List<BexioAccount>? _cachedAccounts;
    private List<BexioTax>? _cachedTaxes;
    private List<BexioPaymentType>? _cachedPaymentTypes;
    private List<BexioArticle> _cachedArticles = [];

    public BexioApiClient(HttpClient httpClient, string apiToken, int? accountId, int? taxId, string language = "de", int? paymentTypeId = null)
    {
        _httpClient = httpClient;
        _apiToken = apiToken;
        _accountId = accountId;
        _taxId = taxId;
        _paymentTypeId = paymentTypeId;
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

    public async Task<BexioContact?> FindContactByNrAsync(string customerNumber)
    {
        if (string.IsNullOrWhiteSpace(customerNumber)) return null;

        var searchPayload = new[]
        {
            new BexioSearchCriteria { Field = "nr", Value = customerNumber.Trim(), Criteria = "=" }
        };

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/contact/search", body));

        if (response.IsSuccessStatusCode)
        {
            var contacts = await response.Content.ReadFromJsonAsync<List<BexioContact>>();
            if (contacts != null && contacts.Count > 0)
            {
                return contacts[0];
            }
        }

        return null;
    }

    public async Task<BexioOrder?> FindOrderByDocumentNrAsync(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return null;

        var searchPayload = new[]
        {
            new BexioSearchCriteria { Field = "document_nr", Value = orderNumber.Trim(), Criteria = "=" }
        };

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/kb_order/search", body));

        if (response.IsSuccessStatusCode)
        {
            var orders = await response.Content.ReadFromJsonAsync<List<BexioOrder>>();
            if (orders != null && orders.Count > 0)
            {
                return orders[0];
            }
        }

        return null;
    }

    public async Task<BexioContact?> FindContactByEmailAsync(string email)
    {
        var searchPayload = new[]
        {
            new BexioSearchCriteria { Field = "mail", Value = email, Criteria = "=" }
        };

        var body = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/contact/search", body));

        if (response.IsSuccessStatusCode)
        {
            var contacts = await response.Content.ReadFromJsonAsync<List<BexioContact>>();
            if (contacts != null && contacts.Count > 0)
            {
                return contacts[0];
            }
        }

        return null;
    }

    public async Task<int> CreateContactAsync(Customer customer)
    {
        var createPayload = new BexioCreateContactRequest
        {
            ContactTypeId = 1, // 1 = Company, 2 = Person
            Name1 = customer.CompanyName,
            Mail = customer.Email,
            StreetName = customer.Street,
            Postcode = customer.ZipCode,
            City = customer.City,
            UserId = 1, // Default userid
            OwnerId = 1 // Default owner
        };

        var body = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
        var createResponse = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/contact", body));
        createResponse.EnsureSuccessStatusCode();

        var newContact = await createResponse.Content.ReadFromJsonAsync<BexioContact>();
        return newContact?.Id ?? throw new InvalidOperationException("Bexio returned an empty response when creating a contact.");
    }

    public async Task<BexioOrder> CreateOrderAsync(int contactId, Order order)
    {
        var orderPayload = new BexioCreateOrderRequest
        {
            ContactId = contactId,
            UserId = 1, // Default user
            Title = string.IsNullOrEmpty(order.Title) ? $"Order: {order.Customer.CompanyName}" : order.Title,
            MwstType = 0, // 0 = excl. VAT, 1 = incl. VAT
            CurrencyId = 1, // 1 = CHF
            PaymentTypeId = _paymentTypeId ?? 1, // the import always passes the selected payment type
            LanguageId = 1, // German
            ApiReference = "Excel-Import",
            Positions = [] // Positions added subsequently
        };

        var body = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, "2.0/kb_order", body));
        response.EnsureSuccessStatusCode();

        var createdOrder = await response.Content.ReadFromJsonAsync<BexioOrder>();
        return createdOrder ?? throw new InvalidOperationException("Bexio returned an empty response when creating an order.");
    }

    public async Task<BexioContact?> GetContactDetailsAsync(int contactId)
    {
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, $"2.0/contact/{contactId}"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BexioContact>();
    }

    public async Task<IReadOnlyList<BexioArticle>> FindArticlesByInternCodeAsync(string internCode)
    {
        if (string.IsNullOrWhiteSpace(internCode)) return [];
        internCode = internCode.Trim();

        lock (_cacheLock)
        {
            if (_cachedArticles.Any(x => x.Code.Equals(internCode, StringComparison.OrdinalIgnoreCase)))
                return [.. _cachedArticles.Where(x => x.Code.Equals(internCode, StringComparison.OrdinalIgnoreCase))];
        }

        var articles = await SearchArticlesAsync([new BexioSearchCriteria { Field = "intern_code", Value = internCode, Criteria = "=" }]);
        if (articles.Count > 0)
        {
            lock (_cacheLock)
            {
                _cachedArticles = [.. _cachedArticles.UnionBy(articles, x => x.Id)];
            }
        }
        return articles;
    }

    public async Task<IReadOnlyList<BexioArticle>> SearchArticlesAsync(IReadOnlyList<BexioSearchCriteria> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // Bexio returns 500 results by default and at most 2000 per page; a full page means there may be more
        const int pageSize = 2000;
        var articles = new List<BexioArticle>();
        while (true)
        {
            var body = new StringContent(JsonSerializer.Serialize(criteria), Encoding.UTF8, "application/json");
            var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Post, $"2.0/article/search?limit={pageSize}&offset={articles.Count}", body));
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var page = await response.Content.ReadFromJsonAsync<List<BexioArticle>>() ?? [];
            articles.AddRange(page);
            if (page.Count < pageSize)
            {
                return articles;
            }
        }
    }
    public async Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position, string? positionText = null)
    {
        var text = !string.IsNullOrEmpty(positionText)
            ? positionText
            : $"Color: {position.Color}, Size: {position.Size}";

        var positionPayload = new BexioCreateArticlePositionRequest
        {
            Amount = position.Quantity,
            ArticleId = articleId,
            Text = text,
            UnitPrice = position.GrossUnitPrice,
            AccountId = _accountId,
            TaxId = _taxId,
            DiscountInPercent = position.DiscountPercent
        };

        var body = new StringContent(JsonSerializer.Serialize(positionPayload), Encoding.UTF8, "application/json");
        var request = CreateRequest(HttpMethod.Post, $"2.0/kb_order/{orderId}/kb_position_article", body);
        var response = await SendWithRateLimitCheckAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Error adding article position to order {orderId}: {errorContent}", null, response.StatusCode);
        }
    }

    public async Task AddDiscountPositionAsync(int orderId, decimal discountPercent, string text = "Rabatt")
    {
        var discountPayload = new BexioCreateDiscountPositionRequest
        {
            Text = string.IsNullOrEmpty(text) ? "Rabatt" : text,
            Value = discountPercent,
            IsPercentual = true
        };

        var body = new StringContent(JsonSerializer.Serialize(discountPayload), Encoding.UTF8, "application/json");
        var request = CreateRequest(HttpMethod.Post, $"2.0/kb_order/{orderId}/kb_position_discount", body);
        var response = await SendWithRateLimitCheckAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Error adding discount position to order {orderId}: {errorContent}", null, response.StatusCode);
        }
    }

    public async Task<int> DeleteAllPositionsAsync(int orderId)
    {
        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, $"2.0/kb_order/{orderId}"));
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Error reading the positions of order {orderId}: {errorContent}", null, response.StatusCode);
        }

        var order = await response.Content.ReadFromJsonAsync<BexioOrder>();
        int deleted = 0;
        foreach (var position in order?.Positions ?? [])
        {
            // The type names the endpoint, eg. KbPositionArticle -> kb_position_article
            string endpoint = JsonNamingPolicy.SnakeCaseLower.ConvertName(position.Type);
            var deleteResponse = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Delete, $"2.0/kb_order/{orderId}/{endpoint}/{position.Id}"));

            // Positions of a sub position group are deleted with the group
            if (deleteResponse.StatusCode == System.Net.HttpStatusCode.NotFound) continue;

            if (!deleteResponse.IsSuccessStatusCode)
            {
                var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error deleting position {position.Id} of order {orderId}: {errorContent}", null, deleteResponse.StatusCode);
            }
            deleted++;
        }
        return deleted;
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
        lock (_cacheLock)
        {
            if (_cachedAccounts != null) return _cachedAccounts;
        }

        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, "2.0/accounts"));
        response.EnsureSuccessStatusCode();
        var accounts = await response.Content.ReadFromJsonAsync<List<BexioAccount>>();

        lock (_cacheLock)
        {
            _cachedAccounts = accounts?.Where(a => a.IsActive && a.AccountType == 1).ToList() ?? [];
            return _cachedAccounts;
        }
    }

    public async Task<List<BexioTax>> GetTaxesAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedTaxes != null) return _cachedTaxes;
        }

        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, "3.0/taxes"));
        response.EnsureSuccessStatusCode();
        var taxes = await response.Content.ReadFromJsonAsync<List<BexioTax>>();

        lock (_cacheLock)
        {
            _cachedTaxes = taxes?.Where(t => t.IsActive && (t.Type == "sales_tax" || t.Type == "not_taxable_turnover")).ToList() ?? [];
            return _cachedTaxes;
        }
    }

    public async Task<List<BexioPaymentType>> GetPaymentTypesAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedPaymentTypes != null) return _cachedPaymentTypes;
        }

        var response = await SendWithRateLimitCheckAsync(CreateRequest(HttpMethod.Get, "2.0/payment_type"));
        response.EnsureSuccessStatusCode();
        var paymentTypes = await response.Content.ReadFromJsonAsync<List<BexioPaymentType>>();

        lock (_cacheLock)
        {
            _cachedPaymentTypes = paymentTypes ?? [];
            return _cachedPaymentTypes;
        }
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

            var clonedRequest = await CloneRequestAsync(request);
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

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);
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
