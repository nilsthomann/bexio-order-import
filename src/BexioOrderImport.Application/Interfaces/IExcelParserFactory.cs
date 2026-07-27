using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Application.Interfaces;

public interface IExcelParserFactory
{
    IExcelParser Create(ExcelMappingOptions options);
}
