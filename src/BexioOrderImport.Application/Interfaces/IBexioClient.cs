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
    /// Fetches the contact email associated with an existing Bexio order ID.
    /// </summary>
    Task<string?> GetOrderContactEmailAsync(int orderId);

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
    Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position);

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
