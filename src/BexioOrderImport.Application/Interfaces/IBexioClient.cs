using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Interfaces;

public interface IBexioClient
{
    Task<int?> FindContactIdAsync(string email);
    Task<int> CreateContactAsync(Customer customer);
    Task<int> CreateOrderAsync(int contactId, Order order);
    Task<int?> FindArticleIdAsync(string articleNumber);
    Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position);
    Task<bool> CheckConnectionAsync();
    Task<List<BexioAccount>> GetAccountsAsync();
    Task<List<BexioTax>> GetTaxesAsync();
}
