using System.Collections.ObjectModel;
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
    private string _newExternalSeason = string.Empty;
    private string _newSeasonCode = string.Empty;

    public SgdProfileConfigViewModel()
    {
        AddSeasonMappingCommand = new RelayCommand(AddSeasonMapping, () => !string.IsNullOrWhiteSpace(NewExternalSeason) && !string.IsNullOrWhiteSpace(NewSeasonCode));
        RemoveSeasonMappingCommand = new RelayCommand<SgdSeasonMapping>(mapping => SeasonMappings.Remove(mapping));
    }

    public string WorksheetIndex { get; set; } = string.Empty;
    public string StartRow { get; set; } = string.Empty;
    public string ArticleNumberColumn { get; set; } = string.Empty;
    public string ArticleNameColumn { get; set; } = string.Empty;
    public string ColorColumn { get; set; } = string.Empty;
    public string SizeColumn { get; set; } = string.Empty;
    public string QuantityColumn { get; set; } = string.Empty;
    public bool UseExternalOrderNumberColumn { get; set; }
    public string ExternalOrderNumberColumn { get; set; } = string.Empty;
    public string SeasonColumn { get; set; } = string.Empty;
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

    /// <summary>
    /// SGD season names and their Bexio season codes, edited in place. Copies, so a cancelled edit keeps the profile.
    /// </summary>
    public ObservableCollection<SgdSeasonMapping> SeasonMappings { get; } = [];

    public string NewExternalSeason
    {
        get => _newExternalSeason;
        set => SetProperty(ref _newExternalSeason, value ?? string.Empty);
    }

    public string NewSeasonCode
    {
        get => _newSeasonCode;
        set => SetProperty(ref _newSeasonCode, value ?? string.Empty);
    }

    public RelayCommand AddSeasonMappingCommand { get; }

    public RelayCommand<SgdSeasonMapping> RemoveSeasonMappingCommand { get; }

    private void AddSeasonMapping()
    {
        SeasonMappings.Add(new SgdSeasonMapping { ExternalSeason = NewExternalSeason.Trim(), SeasonCode = NewSeasonCode.Trim() });
        NewExternalSeason = string.Empty;
        NewSeasonCode = string.Empty;
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
        UseExternalOrderNumberColumn = settings.UseExternalOrderNumberColumn;
        ExternalOrderNumberColumn = settings.ExternalOrderNumberColumn;
        SeasonColumn = settings.SeasonColumn;
        SeasonMappings.Clear();
        foreach (var mapping in settings.SeasonMappings ?? [])
        {
            SeasonMappings.Add(new SgdSeasonMapping { ExternalSeason = mapping.ExternalSeason, SeasonCode = mapping.SeasonCode });
        }
        NewExternalSeason = string.Empty;
        NewSeasonCode = string.Empty;
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
        AddIfNoColumn(errors, SeasonColumn, SgdTranslations.Settings_SeasonColumn);
        AddSeasonMappingErrors(errors);
        if (DefaultOrderName.Contains("{SeasonCode}", StringComparison.Ordinal))
        {
            errors.Add(SgdTranslations.Validation_SeasonCodePlaceholder);
        }
        if (UseExternalOrderNumberColumn)
        {
            AddIfNoColumn(errors, ExternalOrderNumberColumn, SgdTranslations.Settings_ExternalOrderNumberColumn);
        }
        else if (DefaultOrderName.Contains("{ExternalOrderNumber}", StringComparison.Ordinal))
        {
            errors.Add(SgdTranslations.Validation_ExternalOrderNumberColumnRequired);
        }
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
        _settings.UseExternalOrderNumberColumn = UseExternalOrderNumberColumn;
        _settings.ExternalOrderNumberColumn = ExcelColumnHelper.NormalizeColumnLetter(ExternalOrderNumberColumn, "F");
        _settings.SeasonColumn = ExcelColumnHelper.NormalizeColumnLetter(SeasonColumn, "G");
        _settings.SeasonMappings = [.. SeasonMappings.Select(m => new SgdSeasonMapping { ExternalSeason = m.ExternalSeason.Trim(), SeasonCode = m.SeasonCode.Trim() })];
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

    /// <summary>
    /// The parser needs at least one complete mapping and each SGD season name once (case is ignored).
    /// </summary>
    private void AddSeasonMappingErrors(List<string> errors)
    {
        if (SeasonMappings.Count == 0)
        {
            errors.Add(SgdTranslations.Validation_SeasonMappingRequired);
        }
        if (SeasonMappings.Any(m => string.IsNullOrWhiteSpace(m.ExternalSeason) || string.IsNullOrWhiteSpace(m.SeasonCode)))
        {
            errors.Add(SgdTranslations.Validation_SeasonMappingIncomplete);
        }
        errors.AddRange(SeasonMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalSeason))
            .GroupBy(m => m.ExternalSeason.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => string.Format(SgdTranslations.Validation_DuplicateExternalSeason, g.Key)));
        // Typed but not added with the plus button; saving would drop it silently
        if (!string.IsNullOrWhiteSpace(NewExternalSeason) || !string.IsNullOrWhiteSpace(NewSeasonCode))
        {
            errors.Add(SgdTranslations.Validation_SeasonMappingNotAdded);
        }
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
