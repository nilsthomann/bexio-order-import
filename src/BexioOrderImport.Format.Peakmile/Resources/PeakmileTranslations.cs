using System.Resources;

namespace BexioOrderImport.Format.Peakmile.Resources;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class PeakmileTranslations
{
    private static readonly ResourceManager ResourceManager =
        new ResourceManager("BexioOrderImport.Format.Peakmile.Resources.PeakmileTranslations", typeof(PeakmileTranslations).Assembly);

    public static string Format_DisplayName => ResourceManager.GetString(nameof(Format_DisplayName)) ?? string.Empty;
    public static string Format_Description => ResourceManager.GetString(nameof(Format_Description)) ?? string.Empty;
    public static string Import_OrderHeader => ResourceManager.GetString(nameof(Import_OrderHeader)) ?? string.Empty;
    public static string Import_Customer => ResourceManager.GetString(nameof(Import_Customer)) ?? string.Empty;
    public static string Import_Address => ResourceManager.GetString(nameof(Import_Address)) ?? string.Empty;
    public static string Import_Email => ResourceManager.GetString(nameof(Import_Email)) ?? string.Empty;
    public static string Import_Buyer => ResourceManager.GetString(nameof(Import_Buyer)) ?? string.Empty;
    public static string Import_OrderNumber => ResourceManager.GetString(nameof(Import_OrderNumber)) ?? string.Empty;
    public static string Import_Conditions => ResourceManager.GetString(nameof(Import_Conditions)) ?? string.Empty;
    public static string Import_PositionsHeader => ResourceManager.GetString(nameof(Import_PositionsHeader)) ?? string.Empty;
    public static string Import_ColArtNum => ResourceManager.GetString(nameof(Import_ColArtNum)) ?? string.Empty;
    public static string Import_ColName => ResourceManager.GetString(nameof(Import_ColName)) ?? string.Empty;
    public static string Import_ColColor => ResourceManager.GetString(nameof(Import_ColColor)) ?? string.Empty;
    public static string Import_ColCategory => ResourceManager.GetString(nameof(Import_ColCategory)) ?? string.Empty;
    public static string Import_ColSize => ResourceManager.GetString(nameof(Import_ColSize)) ?? string.Empty;
    public static string Import_ColQuantity => ResourceManager.GetString(nameof(Import_ColQuantity)) ?? string.Empty;
    public static string Import_ColUnitPrice => ResourceManager.GetString(nameof(Import_ColUnitPrice)) ?? string.Empty;
    public static string Import_ColTotalPrice => ResourceManager.GetString(nameof(Import_ColTotalPrice)) ?? string.Empty;
    public static string Import_ColDiscount => ResourceManager.GetString(nameof(Import_ColDiscount)) ?? string.Empty;
    public static string Import_ColGrossPrice => ResourceManager.GetString(nameof(Import_ColGrossPrice)) ?? string.Empty;
    public static string Import_ColNetPrice => ResourceManager.GetString(nameof(Import_ColNetPrice)) ?? string.Empty;
    public static string Import_CustomerNumber => ResourceManager.GetString(nameof(Import_CustomerNumber)) ?? string.Empty;
    public static string Settings_EnableRowDiscount => ResourceManager.GetString(nameof(Settings_EnableRowDiscount)) ?? string.Empty;
    public static string Settings_ColRowDiscount => ResourceManager.GetString(nameof(Settings_ColRowDiscount)) ?? string.Empty;
    public static string Settings_HeaderSection => ResourceManager.GetString(nameof(Settings_HeaderSection)) ?? string.Empty;
    public static string Settings_CellCustomerName => ResourceManager.GetString(nameof(Settings_CellCustomerName)) ?? string.Empty;
    public static string Settings_CellStreet => ResourceManager.GetString(nameof(Settings_CellStreet)) ?? string.Empty;
    public static string Settings_CellEmail => ResourceManager.GetString(nameof(Settings_CellEmail)) ?? string.Empty;
    public static string Settings_CellBuyerName => ResourceManager.GetString(nameof(Settings_CellBuyerName)) ?? string.Empty;
    public static string Settings_CellOrderNumber => ResourceManager.GetString(nameof(Settings_CellOrderNumber)) ?? string.Empty;
    public static string Settings_EnableOrderNumber => ResourceManager.GetString(nameof(Settings_EnableOrderNumber)) ?? string.Empty;
    public static string Settings_CellCustomerNumber => ResourceManager.GetString(nameof(Settings_CellCustomerNumber)) ?? string.Empty;
    public static string Settings_EnableCustomerNumber => ResourceManager.GetString(nameof(Settings_EnableCustomerNumber)) ?? string.Empty;
    public static string Settings_CellPaymentTerms => ResourceManager.GetString(nameof(Settings_CellPaymentTerms)) ?? string.Empty;
    public static string Settings_CellDiscount => ResourceManager.GetString(nameof(Settings_CellDiscount)) ?? string.Empty;
    public static string Settings_MatrixSection => ResourceManager.GetString(nameof(Settings_MatrixSection)) ?? string.Empty;
    public static string Settings_MatrixStart => ResourceManager.GetString(nameof(Settings_MatrixStart)) ?? string.Empty;
    public static string Settings_MatrixEnd => ResourceManager.GetString(nameof(Settings_MatrixEnd)) ?? string.Empty;
    public static string Settings_RowStart => ResourceManager.GetString(nameof(Settings_RowStart)) ?? string.Empty;
    public static string Settings_ColsSection => ResourceManager.GetString(nameof(Settings_ColsSection)) ?? string.Empty;
    public static string Settings_ColArtNum => ResourceManager.GetString(nameof(Settings_ColArtNum)) ?? string.Empty;
    public static string Settings_ColName => ResourceManager.GetString(nameof(Settings_ColName)) ?? string.Empty;
    public static string Settings_ColColor => ResourceManager.GetString(nameof(Settings_ColColor)) ?? string.Empty;
    public static string Settings_ColSizeCategory => ResourceManager.GetString(nameof(Settings_ColSizeCategory)) ?? string.Empty;
    public static string Settings_ColStartQty => ResourceManager.GetString(nameof(Settings_ColStartQty)) ?? string.Empty;
    public static string Settings_ColEndQty => ResourceManager.GetString(nameof(Settings_ColEndQty)) ?? string.Empty;
    public static string Settings_ColUnitPrice => ResourceManager.GetString(nameof(Settings_ColUnitPrice)) ?? string.Empty;
    public static string Settings_MatrixCategoryCol => ResourceManager.GetString(nameof(Settings_MatrixCategoryCol)) ?? string.Empty;
    public static string Settings_MatrixStartSizeCol => ResourceManager.GetString(nameof(Settings_MatrixStartSizeCol)) ?? string.Empty;
    public static string Settings_MatrixEndSizeCol => ResourceManager.GetString(nameof(Settings_MatrixEndSizeCol)) ?? string.Empty;
    public static string Settings_WorksheetSection => ResourceManager.GetString(nameof(Settings_WorksheetSection)) ?? string.Empty;
    public static string Settings_TextTemplatesSection => ResourceManager.GetString(nameof(Settings_TextTemplatesSection)) ?? string.Empty;
    public static string Settings_WorksheetLabel => ResourceManager.GetString(nameof(Settings_WorksheetLabel)) ?? string.Empty;
    public static string Settings_DefaultOrderNameLabel => ResourceManager.GetString(nameof(Settings_DefaultOrderNameLabel)) ?? string.Empty;
    public static string Settings_DefaultOrderNameHint => ResourceManager.GetString(nameof(Settings_DefaultOrderNameHint)) ?? string.Empty;
    public static string Settings_SeasonCodeLabel => ResourceManager.GetString(nameof(Settings_SeasonCodeLabel)) ?? string.Empty;
    public static string Settings_SeasonFallbackCodesLabel => ResourceManager.GetString(nameof(Settings_SeasonFallbackCodesLabel)) ?? string.Empty;
    public static string Settings_SeasonFallbackCodesHint => ResourceManager.GetString(nameof(Settings_SeasonFallbackCodesHint)) ?? string.Empty;
    public static string Settings_PositionTextTemplateLabel => ResourceManager.GetString(nameof(Settings_PositionTextTemplateLabel)) ?? string.Empty;
    public static string Settings_DiscountPositionTextTemplateLabel => ResourceManager.GetString(nameof(Settings_DiscountPositionTextTemplateLabel)) ?? string.Empty;
    public static string Settings_DiscountPositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_DiscountPositionTextTemplateHint)) ?? string.Empty;
    public static string Settings_CellZipCodeCity => ResourceManager.GetString(nameof(Settings_CellZipCodeCity)) ?? string.Empty;
    public static string Settings_PositionGroupingModeLabel => ResourceManager.GetString(nameof(Settings_PositionGroupingModeLabel)) ?? string.Empty;
    public static string Settings_PositionGroupingSingle => ResourceManager.GetString(nameof(Settings_PositionGroupingSingle)) ?? string.Empty;
    public static string Settings_PositionGroupingGrouped => ResourceManager.GetString(nameof(Settings_PositionGroupingGrouped)) ?? string.Empty;
    public static string Settings_SinglePositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_SinglePositionTextTemplateHint)) ?? string.Empty;
    public static string Settings_GroupedPositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_GroupedPositionTextTemplateHint)) ?? string.Empty;
    public static string Settings_SizeRowTemplateLabel => ResourceManager.GetString(nameof(Settings_SizeRowTemplateLabel)) ?? string.Empty;
    public static string Settings_SizeRowTemplateHint => ResourceManager.GetString(nameof(Settings_SizeRowTemplateHint)) ?? string.Empty;
    public static string Dialog_SizesRowsPlaceholderRequiredForGrouped => ResourceManager.GetString(nameof(Dialog_SizesRowsPlaceholderRequiredForGrouped)) ?? string.Empty;
    public static string Preview_TotalArticles => ResourceManager.GetString(nameof(Preview_TotalArticles)) ?? string.Empty;
    public static string Preview_TotalGross => ResourceManager.GetString(nameof(Preview_TotalGross)) ?? string.Empty;
    public static string Preview_TotalDiscount => ResourceManager.GetString(nameof(Preview_TotalDiscount)) ?? string.Empty;
    public static string Preview_TotalNet => ResourceManager.GetString(nameof(Preview_TotalNet)) ?? string.Empty;
    public static string Validation_PositiveNumber => ResourceManager.GetString(nameof(Validation_PositiveNumber)) ?? string.Empty;
}
