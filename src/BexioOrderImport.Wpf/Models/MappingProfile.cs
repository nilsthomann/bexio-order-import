using BexioOrderImport.Application.Options;
using BexioOrderImport.Wpf.ViewModels;

namespace BexioOrderImport.Wpf.Models;

public class MappingProfile : ViewModelBase
{
    private string _name = string.Empty;
    private ExcelMappingOptions _mapping = new();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ExcelMappingOptions Mapping
    {
        get => _mapping;
        set => SetProperty(ref _mapping, value);
    }
}

