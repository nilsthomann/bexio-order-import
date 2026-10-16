using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Abstractions.Bexio;

/// <summary>
/// Client interface for interacting with the Bexio REST API.
/// </summary>
public interface IBexioService
{
    /// <summary>
    /// Searches for an existing contact by contact number (nr).
    /// </summary>
    Task<BexioContact?> FindContactByNrAsync(string customerNumber);

    /// <summary>
    /// Searches for an existing order by document number (document_nr).
    /// </summary>
    Task<BexioOrder?> FindOrderByDocumentNrAsync(string orderNumber);

    /// <summary>
    /// Searches for an existing contact by email address.
    /// </summary>
    Task<BexioContact?> FindContactByEmailAsync(string email);

    /// <summary>
    /// Creates a new contact in Bexio for the given customer.
    /// </summary>
    Task<int> CreateContactAsync(Customer customer);

    /// <summary>
    /// Creates a new order header in Bexio.
    /// </summary>
    Task<BexioOrder> CreateOrderAsync(int contactId, Order order);

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
    /// Fallback season codes are tried in order (exact intern code match only) when the season code yields no match.
    /// </summary>
    Task<BexioArticle?> FindArticleAsync(string articleNumber, string color, string seasonCode, IReadOnlyList<string>? fallbackSeasonCodes = null);

    /// <summary>
    /// Finds the articles whose intern_code equals the given code exactly. Results are cached per client.
    /// Returns an empty list if none match.
    /// </summary>
    Task<IReadOnlyList<BexioArticle>> FindArticlesByInternCodeAsync(string internCode);

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
