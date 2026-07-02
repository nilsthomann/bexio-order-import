using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Infrastructure.Bexio;

public class BexioApiClient : IBexioClient
{
    private readonly HttpClient _httpClient;
    private readonly int _accountId; // Standard Buchungskonto für Freipositionen
    private readonly int _taxId;     // Standard MwSt.-Satz ID

    public BexioApiClient(HttpClient httpClient, string apiToken, int accountId = 3200, int taxId = 1)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.bexio.com/2.0/");
        
        // Authorization Header
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        _accountId = accountId;
        _taxId = taxId;
    }

    public async Task<int?> FindContactIdAsync(string email)
    {
        var searchPayload = new[]
        {
            new { field = "mail", value = email, criteria = "=" }
        };

        var searchContent = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("contact/search", searchContent);
        
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
            contact_type_id = 1, // 1 = Firma, 2 = Person
            name_1 = customer.CompanyName,
            address = customer.Street,
            postcode = customer.ZipCode,
            city = customer.City,
            mail = customer.Email,
            owner_id = 1 // Standard-Besitzer
        };

        var createContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
        var createResponse = await _httpClient.PostAsync("contact", createContent);
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
            title = $"Order: {order.Customer.CompanyName}",
            mwst_type = 0, // 0 = excl. VAT, 1 = incl. VAT
            currency_id = 1, // 1 = CHF
            payment_type_id = 1, // Default
            language_id = 1, // German
            api_reference = "Excel-Import",
            positions = new object[0] // Positions added subsequently
        };

        var content = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("kb_order", content);
        response.EnsureSuccessStatusCode();

        var createdOrder = await response.Content.ReadFromJsonAsync<BexioOrder>();
        return createdOrder?.Id ?? throw new Exception("Error creating order in Bexio.");
    }

    public async Task<int?> FindArticleIdAsync(string articleNumber, string articleName)
    {
        // 1. Suche nach intern_code (Artikel Nummer, z.B. "743097")
        var searchPayload = new[]
        {
            new { field = "intern_code", value = articleNumber, criteria = "=" }
        };

        var searchContent = new StringContent(JsonSerializer.Serialize(searchPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("article/search", searchContent);
        
        if (response.IsSuccessStatusCode)
        {
            var articles = await response.Content.ReadFromJsonAsync<List<BexioArticle>>();
            if (articles != null && articles.Count > 0)
            {
                return articles[0].Id;
            }
        }

        // 2. Suche nach intern_name (Bezeichnung) als Fallback
        var searchNamePayload = new[]
        {
            new { field = "intern_name", value = articleName, criteria = "=" }
        };

        var searchNameContent = new StringContent(JsonSerializer.Serialize(searchNamePayload), Encoding.UTF8, "application/json");
        var nameResponse = await _httpClient.PostAsync("article/search", searchNameContent);
        
        if (nameResponse.IsSuccessStatusCode)
        {
            var articles = await nameResponse.Content.ReadFromJsonAsync<List<BexioArticle>>();
            if (articles != null && articles.Count > 0)
            {
                return articles[0].Id;
            }
        }

        return null;
    }

    public async Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position)
    {
        var positionPayload = new
        {
            amount = position.Quantity,
            article_id = articleId,
            type = "KbPositionArticle",
            text = $"Farbe: {position.Color}, Grösse: {position.Size}", // Spezifische Details als Zusatztext
            unit_price = position.UnitPrice, // Überschreibe den Basispreis mit dem spezifischen Preis aus Excel
            discount_in_percent = position.DiscountPercent
        };

        var content = new StringContent(JsonSerializer.Serialize(positionPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"kb_order/{orderId}/kb_position_article", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddCustomPositionAsync(int orderId, OrderPosition position)
    {
        var positionPayload = new
        {
            amount = position.Quantity,
            text = $"{position.ArticleNumber} - {position.ArticleName} (Grösse: {position.Size}, Farbe: {position.Color})",
            unit_price = position.UnitPrice,
            account_id = _accountId,
            tax_id = _taxId,
            discount_in_percent = position.DiscountPercent
        };

        var content = new StringContent(JsonSerializer.Serialize(positionPayload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"kb_order/{orderId}/kb_position_custom", content);
        response.EnsureSuccessStatusCode();
    }

    // Hilfsklassen für JSON-Deserialisierung
    private class BexioContact { public int Id { get; set; } }
    private class BexioOrder { public int Id { get; set; } }
    private class BexioArticle { public int Id { get; set; } }
}
