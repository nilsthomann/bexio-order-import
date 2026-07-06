using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.Services;

public class InMemoryExcelParser : IExcelParser
{
    private readonly Order _order;
    public InMemoryExcelParser(Order order) { _order = order; }
    public Order ParseOrderForm(string filePath) => _order;
}
