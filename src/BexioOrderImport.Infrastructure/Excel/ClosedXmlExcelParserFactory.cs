using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Options;
using Microsoft.Extensions.Options;

namespace BexioOrderImport.Infrastructure.Excel;

public class ClosedXmlExcelParserFactory : IExcelParserFactory
{
    public IExcelParser Create(ExcelMappingOptions options)
    {
        return new ClosedXmlExcelParser(Microsoft.Extensions.Options.Options.Create(options));
    }
}
