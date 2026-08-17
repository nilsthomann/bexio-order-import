using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Application.Interfaces;

/// <summary>
/// Client interface for interacting with the Bexio REST API.
/// </summary>
public interface IBexioClient
{
    /// <summary>
    /// Searches for an existing contact by email address.
    /// </summary>
    Task<int?> FindContactIdAsync(string email);

    /// <summary>
    /// Creates a new contact in Bexio for the given customer.
    /// </summary>
    Task<int> CreateContactAsync(Customer customer);

    /// <summary>
    /// Creates a new order header in Bexio.
    /// </summary>
    Task<int> CreateOrderAsync(int contactId, Order order);

    /// <summary>
    /// Fetches contact details associated with an existing Bexio order ID.
    /// </summary>
    Task<BexioContact?> GetOrderContactDetailsAsync(int orderId);

    /// <summary>
    /// Fetches contact details associated with an existing Bexio contact ID.
    /// </summary>
    Task<BexioContact?> GetContactDetailsAsync(int contactId);

    /// <summary>
    /// Finds a Bexio article matching the article number, color, and season code.
    /// </summary>
    Task<BexioArticle?> FindArticleAsync(string articleNumber, string color, string seasonCode);

    /// <summary>
    /// Pre-fetches articles from Bexio matching the season code and filters relevant article numbers into cache to accelerate batch imports.
    /// </summary>
    Task PreFetchArticlesAsync(string seasonCode, IEnumerable<string> articleNumbers);

    /// <summary>
    /// Appends an article position to an existing order in Bexio.
    /// </summary>
    /// <param name="positionText">The rendered position text to use. If null or empty, falls back to a default format.</param>
    Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position, string? positionText = null);

    /// <summary>
    /// Appends a global discount position to an existing order in Bexio.
    /// </summary>
    Task AddDiscountPositionAsync(int orderId, decimal discountPercent, string text = "Rabatt");

    /// <summary>
    /// Verifies API connectivity with Bexio.
    /// </summary>
    Task<bool> CheckConnectionAsync();

    /// <summary>
    /// Retrieves active revenue bookkeeping accounts.
    /// </summary>
    Task<List<BexioAccount>> GetAccountsAsync();

    /// <summary>
    /// Retrieves active sales tax rates.
    /// </summary>
    Task<List<BexioTax>> GetTaxesAsync();
}
