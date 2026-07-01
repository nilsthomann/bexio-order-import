namespace BexioOrderImport.Domain.Models;

public class Customer
{
    public string CompanyName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
}
