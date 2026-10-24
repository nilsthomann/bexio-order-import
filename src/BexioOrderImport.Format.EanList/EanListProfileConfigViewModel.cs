using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Format.EanList.Resources;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// Inputs of the EAN list profile settings. Numbers and columns are kept as text so invalid inputs can be reported on save.
/// </summary>
public sealed class EanListProfileConfigViewModel
{
    private EanListSettings _settings = new();

    public string WorksheetIndex { get; set; } = string.Empty;
    public string StartRow { get; set; } = string.Empty;
    public string EanColumn { get; set; } = string.Empty;
    public string QuantityColumn { get; set; } = string.Empty;
    public bool UseSalesUnitColumn { get; set; }
    public string SalesUnitColumn { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public string DefaultOrderName { get; set; } = string.Empty;
    public string PositionTextTemplate { get; set; } = string.Empty;

    public void Load(EanListSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        WorksheetIndex = settings.WorksheetIndex.ToString();
        StartRow = settings.StartRow.ToString();
        EanColumn = settings.EanColumn;
        QuantityColumn = settings.QuantityColumn;
        UseSalesUnitColumn = settings.UseSalesUnitColumn;
        SalesUnitColumn = settings.SalesUnitColumn;
        CustomerNumber = settings.CustomerNumber;
        DefaultOrderName = settings.DefaultOrderName;
        PositionTextTemplate = settings.PositionTextTemplate;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        AddIfNotPositive(errors, WorksheetIndex, EanListTranslations.Settings_WorksheetLabel);
        AddIfNotPositive(errors, StartRow, EanListTranslations.Settings_StartRowLabel);
        AddIfNoColumn(errors, EanColumn, EanListTranslations.Settings_EanColumn);
        AddIfNoColumn(errors, QuantityColumn, EanListTranslations.Settings_QuantityColumn);
        if (UseSalesUnitColumn)
        {
            AddIfNoColumn(errors, SalesUnitColumn, EanListTranslations.Settings_SalesUnitColumn);
        }
        if (string.IsNullOrWhiteSpace(CustomerNumber))
        {
            errors.Add(string.Format(EanListTranslations.Validation_Required, EanListTranslations.Settings_CustomerNumberLabel.TrimEnd(':', ' ')));
        }
        return errors;
    }

    public EanListSettings Save()
    {
        _settings.WorksheetIndex = int.Parse(WorksheetIndex.Trim());
        _settings.StartRow = int.Parse(StartRow.Trim());
        _settings.EanColumn = ExcelColumnHelper.NormalizeColumnLetter(EanColumn, "A");
        _settings.QuantityColumn = ExcelColumnHelper.NormalizeColumnLetter(QuantityColumn, "B");
        _settings.UseSalesUnitColumn = UseSalesUnitColumn;
        _settings.SalesUnitColumn = ExcelColumnHelper.NormalizeColumnLetter(SalesUnitColumn, "C");
        _settings.CustomerNumber = CustomerNumber.Trim();
        _settings.DefaultOrderName = DefaultOrderName.Trim();
        _settings.PositionTextTemplate = PositionTextTemplate.Trim();
        return _settings;
    }

    private static void AddIfNotPositive(List<string> errors, string value, string label)
    {
        if (!int.TryParse(value?.Trim(), out int number) || number <= 0)
        {
            errors.Add(string.Format(EanListTranslations.Validation_PositiveNumber, label.TrimEnd(':', ' ')));
        }
    }

    private static void AddIfNoColumn(List<string> errors, string value, string label)
    {
        // A column is a letter (eg. "AA") or its number (eg. "27")
        string text = value?.Trim() ?? string.Empty;
        bool isColumnNumber = int.TryParse(text, out int number) && number > 0;
        if (!isColumnNumber && !ExcelColumnHelper.IsValidColumnLetter(text))
        {
            errors.Add(string.Format(EanListTranslations.Validation_InvalidColumn, label.TrimEnd(':', ' ')));
        }
    }
}
