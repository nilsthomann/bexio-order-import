using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.Peakmile;

public interface IExcelParser
{
    Order ParseOrderForm(string filePath);
}
