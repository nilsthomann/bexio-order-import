using System.Globalization;
using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Format.Sgd.Resources;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Inputs of the SGD profile settings. Numbers and columns are kept as text so invalid inputs can be reported on save.
/// </summary>
public sealed class SgdProfileConfigViewModel : ViewModelBase
{
    private SgdSettings _settings = new();
    private bool _isGroupedPositions;

    public string WorksheetIndex { get; set; } = string.Empty;
    public string StartRow { get; set; } = string.Empty;
    public string ArticleNumberColumn { get; set; } = string.Empty;
    public string ArticleNameColumn { get; set; } = string.Empty;
    public string ColorColumn { get; set; } = string.Empty;
    public string SizeColumn { get; set; } = string.Empty;
    public string QuantityColumn { get; set; } = string.Empty;
    public string SeasonCode { get; set; } = string.Empty;
    public string SeasonFallbackCodes { get; set; } = string.Empty;
    public string DiscountPercent { get; set; } = string.Empty;
    public string DefaultOrderName { get; set; } = string.Empty;
    public string SinglePositionTextTemplate { get; set; } = string.Empty;
    public string GroupedPositionTextTemplate { get; set; } = string.Empty;
    public string SizeRowTemplate { get; set; } = string.Empty;
    public string DiscountPositionTextTemplate { get; set; } = string.Empty;

    public bool IsGroupedPositions
    {
        get => _isGroupedPositions;
        set
        {
            if (SetProperty(ref _isGroupedPositions, value))
            {
                OnPropertyChanged(nameof(IsSinglePosition));
            }
        }
    }

    public bool IsSinglePosition
    {
        get => !IsGroupedPositions;
        set => IsGroupedPositions = !value;
    }

    public void Load(SgdSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        WorksheetIndex = settings.WorksheetIndex.ToString(CultureInfo.InvariantCulture);
        StartRow = settings.StartRow.ToString(CultureInfo.InvariantCulture);
        ArticleNumberColumn = settings.ArticleNumberColumn;
        ArticleNameColumn = settings.ArticleNameColumn;
        ColorColumn = settings.ColorColumn;
        SizeColumn = settings.SizeColumn;
        QuantityColumn = settings.QuantityColumn;
        SeasonCode = settings.SeasonCode;
        SeasonFallbackCodes = string.Join(", ", settings.SeasonFallbackCodes);
        DiscountPercent = settings.DiscountPercent.ToString(CultureInfo.CurrentCulture);
        DefaultOrderName = settings.DefaultOrderName;
        SinglePositionTextTemplate = settings.SinglePositionTextTemplate;
        GroupedPositionTextTemplate = settings.GroupedPositionTextTemplate;
        SizeRowTemplate = string.IsNullOrWhiteSpace(settings.SizeRowTemplate) ? SgdSettings.DefaultSizeRowTemplate : settings.SizeRowTemplate;
        DiscountPositionTextTemplate = settings.DiscountPositionTextTemplate;
        IsGroupedPositions = settings.PositionGroupingMode == SgdPositionGroupingMode.GroupedSizePosition;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        AddIfNotPositive(errors, WorksheetIndex, SgdTranslations.Settings_WorksheetLabel);
        AddIfNotPositive(errors, StartRow, SgdTranslations.Settings_StartRowLabel);
        AddIfNoColumn(errors, ArticleNumberColumn, SgdTranslations.Settings_ArticleNumberColumn);
        AddIfNoColumn(errors, ArticleNameColumn, SgdTranslations.Settings_ArticleNameColumn);
        AddIfNoColumn(errors, ColorColumn, SgdTranslations.Settings_ColorColumn);
        AddIfNoColumn(errors, SizeColumn, SgdTranslations.Settings_SizeColumn);
        AddIfNoColumn(errors, QuantityColumn, SgdTranslations.Settings_QuantityColumn);
        if (ParseDiscount(DiscountPercent) is not (>= 0 and <= 100))
        {
            errors.Add(SgdTranslations.Validation_InvalidDiscount);
        }
        if (IsGroupedPositions && !GroupedPositionTextTemplate.Contains("{SizesRows}", StringComparison.Ordinal))
        {
            errors.Add(SgdTranslations.Validation_SizesRowsRequired);
        }
        return errors;
    }

    public SgdSettings Save()
    {
        _settings.WorksheetIndex = int.Parse(WorksheetIndex.Trim(), CultureInfo.InvariantCulture);
        _settings.StartRow = int.Parse(StartRow.Trim(), CultureInfo.InvariantCulture);
        _settings.ArticleNumberColumn = ExcelColumnHelper.NormalizeColumnLetter(ArticleNumberColumn, "A");
        _settings.ArticleNameColumn = ExcelColumnHelper.NormalizeColumnLetter(ArticleNameColumn, "B");
        _settings.ColorColumn = ExcelColumnHelper.NormalizeColumnLetter(ColorColumn, "C");
        _settings.SizeColumn = ExcelColumnHelper.NormalizeColumnLetter(SizeColumn, "D");
        _settings.QuantityColumn = ExcelColumnHelper.NormalizeColumnLetter(QuantityColumn, "E");
        _settings.SeasonCode = SeasonCode.Trim();
        _settings.SeasonFallbackCodes = [.. SeasonFallbackCodes.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        _settings.DiscountPercent = ParseDiscount(DiscountPercent) ?? 0m;
        _settings.DefaultOrderName = DefaultOrderName.Trim();
        _settings.PositionGroupingMode = IsGroupedPositions ? SgdPositionGroupingMode.GroupedSizePosition : SgdPositionGroupingMode.SinglePositionPerSize;
        _settings.SinglePositionTextTemplate = SinglePositionTextTemplate.Trim();
        _settings.GroupedPositionTextTemplate = GroupedPositionTextTemplate.Trim();
        _settings.SizeRowTemplate = SizeRowTemplate.Trim();
        _settings.DiscountPositionTextTemplate = DiscountPositionTextTemplate.Trim();
        return _settings;
    }

    /// <summary>
    /// An empty input is 0. Without thousands separators "7.5" can't be read as 75 in cultures with a decimal comma.
    /// </summary>
    private static decimal? ParseDiscount(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (text.Length == 0) return 0m;
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out decimal discount)
            || decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out discount)
            ? discount
            : null;
    }

    private static void AddIfNotPositive(List<string> errors, string value, string label)
    {
        if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number <= 0)
        {
            errors.Add(string.Format(SgdTranslations.Validation_PositiveNumber, label.TrimEnd(':', ' ')));
        }
    }

    private static void AddIfNoColumn(List<string> errors, string value, string label)
    {
        // A column is a letter (eg. "AA") or its number (eg. "27")
        string text = value?.Trim() ?? string.Empty;
        bool isColumnNumber = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number > 0;
        if (!isColumnNumber && !ExcelColumnHelper.IsValidColumnLetter(text))
        {
            errors.Add(string.Format(SgdTranslations.Validation_InvalidColumn, label.TrimEnd(':', ' ')));
        }
    }
}
