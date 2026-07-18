using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Application.Interfaces;

public interface IBexioClient
{
    Task<int?> FindContactIdAsync(string email);
    Task<int> CreateContactAsync(Customer customer);
    Task<int> CreateOrderAsync(int contactId, Order order);
    Task<string?> GetOrderContactEmailAsync(int orderId);
    Task<BexioArticle?> FindArticleAsync(string articleNumber, string color, string seasonCode);
    Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position);
    Task<bool> CheckConnectionAsync();
    Task<List<BexioAccount>> GetAccountsAsync();
    Task<List<BexioTax>> GetTaxesAsync();
}
