using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Format.Peakmile.Resources;

namespace BexioOrderImport.Format.Peakmile;

/// <summary>
/// Inputs of the Peakmile profile settings. Numbers are kept as text so invalid inputs can be reported on save.
/// </summary>
public sealed class PeakmileProfileConfigViewModel : ViewModelBase
{
    private PeakmileSettings _settings = new();
    private bool _isGroupedPositions;

    public string WorksheetIndex { get; set; } = string.Empty;
    public string SeasonCode { get; set; } = string.Empty;
    public string SeasonFallbackCodes { get; set; } = string.Empty;

    public string CompanyNameCell { get; set; } = string.Empty;
    public string StreetCell { get; set; } = string.Empty;
    public string ZipCityCell { get; set; } = string.Empty;
    public string BuyerEmailCell { get; set; } = string.Empty;
    public string BuyerNameCell { get; set; } = string.Empty;
    public bool EnableOrderNumber { get; set; }
    public string OrderNumberCell { get; set; } = string.Empty;
    public bool EnableCustomerNumber { get; set; }
    public string CustomerNumberCell { get; set; } = string.Empty;
    public string PaymentTermsCell { get; set; } = string.Empty;
    public string DiscountCell { get; set; } = string.Empty;

    public string MatrixStartRow { get; set; } = string.Empty;
    public string MatrixEndRow { get; set; } = string.Empty;
    public string MatrixCategoryColumn { get; set; } = string.Empty;
    public string MatrixStartSizeColumn { get; set; } = string.Empty;
    public string MatrixEndSizeColumn { get; set; } = string.Empty;

    public string DataStartRow { get; set; } = string.Empty;
    public string ArticleNumberColumn { get; set; } = string.Empty;
    public string ArticleNameColumn { get; set; } = string.Empty;
    public string ColorColumn { get; set; } = string.Empty;
    public string CategoryColumn { get; set; } = string.Empty;
    public string StartQtyColumn { get; set; } = string.Empty;
    public string EndQtyColumn { get; set; } = string.Empty;
    public string UnitPriceColumn { get; set; } = string.Empty;
    public bool EnableRowDiscount { get; set; }
    public string RowDiscountColumn { get; set; } = string.Empty;

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

    public void Load(PeakmileSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        WorksheetIndex = settings.WorksheetIndex.ToString();
        SeasonCode = settings.SeasonCode;
        SeasonFallbackCodes = string.Join(", ", settings.SeasonFallbackCodes);

        CompanyNameCell = settings.Header.CompanyNameCell;
        StreetCell = settings.Header.StreetCell;
        ZipCityCell = settings.Header.ZipCityCell;
        BuyerEmailCell = settings.Header.BuyerEmailCell;
        BuyerNameCell = settings.Header.BuyerNameCell;
        EnableOrderNumber = settings.Header.EnableOrderNumber;
        OrderNumberCell = settings.Header.OrderNumberCell;
        EnableCustomerNumber = settings.Header.EnableCustomerNumber;
        CustomerNumberCell = settings.Header.CustomerNumberCell;
        PaymentTermsCell = settings.Header.PaymentTermsCell;
        DiscountCell = settings.Header.DiscountCell;

        MatrixStartRow = settings.SizeMatrix.StartRow.ToString();
        MatrixEndRow = settings.SizeMatrix.EndRow.ToString();
        MatrixCategoryColumn = settings.SizeMatrix.CategoryColumn;
        MatrixStartSizeColumn = settings.SizeMatrix.StartSizeColumn;
        MatrixEndSizeColumn = settings.SizeMatrix.EndSizeColumn;

        DataStartRow = settings.Data.StartRow.ToString();
        ArticleNumberColumn = settings.Data.ArticleNumberColumn;
        ArticleNameColumn = settings.Data.ArticleNameColumn;
        ColorColumn = settings.Data.ColorColumn;
        CategoryColumn = settings.Data.CategoryColumn;
        StartQtyColumn = settings.Data.StartQtyColumn;
        EndQtyColumn = settings.Data.EndQtyColumn;
        UnitPriceColumn = settings.Data.UnitPriceColumn;
        EnableRowDiscount = settings.Data.EnableRowDiscount;
        RowDiscountColumn = settings.Data.RowDiscountColumn;

        DefaultOrderName = settings.DefaultOrderName;
        SinglePositionTextTemplate = settings.SinglePositionTextTemplate;
        GroupedPositionTextTemplate = settings.GroupedPositionTextTemplate;
        SizeRowTemplate = string.IsNullOrWhiteSpace(settings.SizeRowTemplate) ? "{Amount}x Size {Size}" : settings.SizeRowTemplate;
        DiscountPositionTextTemplate = settings.DiscountPositionTextTemplate;
        IsGroupedPositions = settings.PositionGroupingMode == PositionGroupingMode.GroupedSizePosition;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (IsGroupedPositions && !GroupedPositionTextTemplate.Contains("{SizesRows}"))
        {
            errors.Add(PeakmileTranslations.Dialog_SizesRowsPlaceholderRequiredForGrouped);
        }

        AddIfNotPositive(errors, WorksheetIndex, PeakmileTranslations.Settings_WorksheetLabel);
        AddIfNotPositive(errors, MatrixStartRow, PeakmileTranslations.Settings_MatrixStart);
        AddIfNotPositive(errors, MatrixEndRow, PeakmileTranslations.Settings_MatrixEnd);
        AddIfNotPositive(errors, DataStartRow, PeakmileTranslations.Settings_RowStart);

        return errors;
    }

    public PeakmileSettings Save()
    {
        var s = _settings;

        s.WorksheetIndex = int.Parse(WorksheetIndex.Trim());
        s.SeasonCode = SeasonCode.Trim();
        s.SeasonFallbackCodes = [.. SeasonFallbackCodes.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        s.Header.CompanyNameCell = CompanyNameCell.Trim();
        s.Header.StreetCell = StreetCell.Trim();
        s.Header.ZipCityCell = ZipCityCell.Trim();
        s.Header.BuyerEmailCell = BuyerEmailCell.Trim();
        s.Header.BuyerNameCell = BuyerNameCell.Trim();
        s.Header.EnableOrderNumber = EnableOrderNumber;
        s.Header.OrderNumberCell = OrderNumberCell.Trim();
        s.Header.EnableCustomerNumber = EnableCustomerNumber;
        s.Header.CustomerNumberCell = CustomerNumberCell.Trim();
        s.Header.PaymentTermsCell = PaymentTermsCell.Trim();
        s.Header.DiscountCell = DiscountCell.Trim();

        s.SizeMatrix.StartRow = int.Parse(MatrixStartRow.Trim());
        s.SizeMatrix.EndRow = int.Parse(MatrixEndRow.Trim());
        s.SizeMatrix.CategoryColumn = ExcelColumnHelper.NormalizeColumnLetter(MatrixCategoryColumn, "D");
        s.SizeMatrix.StartSizeColumn = ExcelColumnHelper.NormalizeColumnLetter(MatrixStartSizeColumn, "E");
        s.SizeMatrix.EndSizeColumn = ExcelColumnHelper.NormalizeColumnLetter(MatrixEndSizeColumn, "R");

        s.Data.StartRow = int.Parse(DataStartRow.Trim());
        s.Data.ArticleNumberColumn = ExcelColumnHelper.NormalizeColumnLetter(ArticleNumberColumn, "A");
        s.Data.ArticleNameColumn = ExcelColumnHelper.NormalizeColumnLetter(ArticleNameColumn, "B");
        s.Data.ColorColumn = ExcelColumnHelper.NormalizeColumnLetter(ColorColumn, "C");
        s.Data.CategoryColumn = ExcelColumnHelper.NormalizeColumnLetter(CategoryColumn, "D");
        s.Data.StartQtyColumn = ExcelColumnHelper.NormalizeColumnLetter(StartQtyColumn, "E");
        s.Data.EndQtyColumn = ExcelColumnHelper.NormalizeColumnLetter(EndQtyColumn, "R");
        s.Data.UnitPriceColumn = ExcelColumnHelper.NormalizeColumnLetter(UnitPriceColumn, "T");
        s.Data.EnableRowDiscount = EnableRowDiscount;
        s.Data.RowDiscountColumn = ExcelColumnHelper.NormalizeColumnLetter(RowDiscountColumn, "U");

        s.DefaultOrderName = DefaultOrderName.Trim();
        s.PositionGroupingMode = IsGroupedPositions ? PositionGroupingMode.GroupedSizePosition : PositionGroupingMode.SinglePositionPerSize;
        s.SinglePositionTextTemplate = SinglePositionTextTemplate.Trim();
        s.GroupedPositionTextTemplate = GroupedPositionTextTemplate.Trim();
        s.SizeRowTemplate = SizeRowTemplate.Trim();
        s.DiscountPositionTextTemplate = DiscountPositionTextTemplate.Trim();

        return s;
    }

    private static void AddIfNotPositive(List<string> errors, string value, string label)
    {
        if (!int.TryParse(value?.Trim(), out int number) || number <= 0)
        {
            errors.Add(string.Format(PeakmileTranslations.Validation_PositiveNumber, label.TrimEnd(':', ' ')));
        }
    }
}
