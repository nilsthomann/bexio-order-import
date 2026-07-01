using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Interfaces;

public interface IExcelParser
{
    Order ParseOrderForm(string filePath);
}
