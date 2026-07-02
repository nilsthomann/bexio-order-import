using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Wpf.Models;

public class MappingProfile
{
    public string Name { get; set; } = string.Empty;
    public ExcelMappingOptions Mapping { get; set; } = new();
}
