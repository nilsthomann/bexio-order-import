using System.Resources;

namespace BexioOrderImport.Format.Sgd.Resources;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class SgdTranslations
{
    private static readonly ResourceManager ResourceManager =
        new("BexioOrderImport.Format.Sgd.Resources.SgdTranslations", typeof(SgdTranslations).Assembly);

    public static string Format_DisplayName => ResourceManager.GetString(nameof(Format_DisplayName)) ?? string.Empty;
    public static string Format_Description => ResourceManager.GetString(nameof(Format_Description)) ?? string.Empty;
    public static string Settings_SourceSection => ResourceManager.GetString(nameof(Settings_SourceSection)) ?? string.Empty;
    public static string Settings_WorksheetLabel => ResourceManager.GetString(nameof(Settings_WorksheetLabel)) ?? string.Empty;
    public static string Settings_StartRowLabel => ResourceManager.GetString(nameof(Settings_StartRowLabel)) ?? string.Empty;
    public static string Settings_StartRowHint => ResourceManager.GetString(nameof(Settings_StartRowHint)) ?? string.Empty;
    public static string Settings_ArticleNumberColumn => ResourceManager.GetString(nameof(Settings_ArticleNumberColumn)) ?? string.Empty;
    public static string Settings_ArticleNameColumn => ResourceManager.GetString(nameof(Settings_ArticleNameColumn)) ?? string.Empty;
    public static string Settings_ColorColumn => ResourceManager.GetString(nameof(Settings_ColorColumn)) ?? string.Empty;
    public static string Settings_SizeColumn => ResourceManager.GetString(nameof(Settings_SizeColumn)) ?? string.Empty;
    public static string Settings_QuantityColumn => ResourceManager.GetString(nameof(Settings_QuantityColumn)) ?? string.Empty;
    public static string Settings_RowsHint => ResourceManager.GetString(nameof(Settings_RowsHint)) ?? string.Empty;
    public static string Settings_ArticleSection => ResourceManager.GetString(nameof(Settings_ArticleSection)) ?? string.Empty;
    public static string Settings_SeasonCodeLabel => ResourceManager.GetString(nameof(Settings_SeasonCodeLabel)) ?? string.Empty;
    public static string Settings_SeasonCodeHint => ResourceManager.GetString(nameof(Settings_SeasonCodeHint)) ?? string.Empty;
    public static string Settings_SeasonFallbackCodesLabel => ResourceManager.GetString(nameof(Settings_SeasonFallbackCodesLabel)) ?? string.Empty;
    public static string Settings_SeasonFallbackCodesHint => ResourceManager.GetString(nameof(Settings_SeasonFallbackCodesHint)) ?? string.Empty;
    public static string Settings_DiscountLabel => ResourceManager.GetString(nameof(Settings_DiscountLabel)) ?? string.Empty;
    public static string Settings_DiscountHint => ResourceManager.GetString(nameof(Settings_DiscountHint)) ?? string.Empty;
    public static string Settings_TextTemplatesSection => ResourceManager.GetString(nameof(Settings_TextTemplatesSection)) ?? string.Empty;
    public static string Settings_DefaultOrderNameLabel => ResourceManager.GetString(nameof(Settings_DefaultOrderNameLabel)) ?? string.Empty;
    public static string Settings_DefaultOrderNameHint => ResourceManager.GetString(nameof(Settings_DefaultOrderNameHint)) ?? string.Empty;
    public static string Settings_PositionGroupingModeLabel => ResourceManager.GetString(nameof(Settings_PositionGroupingModeLabel)) ?? string.Empty;
    public static string Settings_PositionGroupingSingle => ResourceManager.GetString(nameof(Settings_PositionGroupingSingle)) ?? string.Empty;
    public static string Settings_PositionGroupingGrouped => ResourceManager.GetString(nameof(Settings_PositionGroupingGrouped)) ?? string.Empty;
    public static string Settings_PositionTextTemplateLabel => ResourceManager.GetString(nameof(Settings_PositionTextTemplateLabel)) ?? string.Empty;
    public static string Settings_SinglePositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_SinglePositionTextTemplateHint)) ?? string.Empty;
    public static string Settings_GroupedPositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_GroupedPositionTextTemplateHint)) ?? string.Empty;
    public static string Settings_SizeRowTemplateLabel => ResourceManager.GetString(nameof(Settings_SizeRowTemplateLabel)) ?? string.Empty;
    public static string Settings_SizeRowTemplateHint => ResourceManager.GetString(nameof(Settings_SizeRowTemplateHint)) ?? string.Empty;
    public static string Settings_DiscountPositionTextTemplateLabel => ResourceManager.GetString(nameof(Settings_DiscountPositionTextTemplateLabel)) ?? string.Empty;
    public static string Settings_DiscountPositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_DiscountPositionTextTemplateHint)) ?? string.Empty;
    public static string Validation_PositiveNumber => ResourceManager.GetString(nameof(Validation_PositiveNumber)) ?? string.Empty;
    public static string Validation_InvalidColumn => ResourceManager.GetString(nameof(Validation_InvalidColumn)) ?? string.Empty;
    public static string Validation_InvalidDiscount => ResourceManager.GetString(nameof(Validation_InvalidDiscount)) ?? string.Empty;
    public static string Validation_SizesRowsRequired => ResourceManager.GetString(nameof(Validation_SizesRowsRequired)) ?? string.Empty;
    public static string Error_MissingArticleNumber => ResourceManager.GetString(nameof(Error_MissingArticleNumber)) ?? string.Empty;
    public static string Error_MissingColor => ResourceManager.GetString(nameof(Error_MissingColor)) ?? string.Empty;
    public static string Error_InvalidQuantity => ResourceManager.GetString(nameof(Error_InvalidQuantity)) ?? string.Empty;
    public static string Preview_Header => ResourceManager.GetString(nameof(Preview_Header)) ?? string.Empty;
    public static string Preview_SeasonCode => ResourceManager.GetString(nameof(Preview_SeasonCode)) ?? string.Empty;
    public static string Preview_Discount => ResourceManager.GetString(nameof(Preview_Discount)) ?? string.Empty;
    public static string Preview_CustomerHint => ResourceManager.GetString(nameof(Preview_CustomerHint)) ?? string.Empty;
    public static string Preview_PositionsHeader => ResourceManager.GetString(nameof(Preview_PositionsHeader)) ?? string.Empty;
    public static string Preview_ColRow => ResourceManager.GetString(nameof(Preview_ColRow)) ?? string.Empty;
    public static string Preview_ColArticleNumber => ResourceManager.GetString(nameof(Preview_ColArticleNumber)) ?? string.Empty;
    public static string Preview_ColArticleName => ResourceManager.GetString(nameof(Preview_ColArticleName)) ?? string.Empty;
    public static string Preview_ColColor => ResourceManager.GetString(nameof(Preview_ColColor)) ?? string.Empty;
    public static string Preview_ColSize => ResourceManager.GetString(nameof(Preview_ColSize)) ?? string.Empty;
    public static string Preview_ColQuantity => ResourceManager.GetString(nameof(Preview_ColQuantity)) ?? string.Empty;
    public static string Preview_Positions => ResourceManager.GetString(nameof(Preview_Positions)) ?? string.Empty;
    public static string Preview_TotalQuantity => ResourceManager.GetString(nameof(Preview_TotalQuantity)) ?? string.Empty;
    public static string Preview_SkippedRows => ResourceManager.GetString(nameof(Preview_SkippedRows)) ?? string.Empty;
    public static string Preview_PricesHint => ResourceManager.GetString(nameof(Preview_PricesHint)) ?? string.Empty;
    public static string Import_CustomerTitle => ResourceManager.GetString(nameof(Import_CustomerTitle)) ?? string.Empty;
    public static string Import_CustomerMessage => ResourceManager.GetString(nameof(Import_CustomerMessage)) ?? string.Empty;
    public static string Import_CustomerRequired => ResourceManager.GetString(nameof(Import_CustomerRequired)) ?? string.Empty;
    public static string Import_CustomerNotFound => ResourceManager.GetString(nameof(Import_CustomerNotFound)) ?? string.Empty;
    public static string Import_OrderChoiceTitle => ResourceManager.GetString(nameof(Import_OrderChoiceTitle)) ?? string.Empty;
    public static string Import_OrderChoiceMessage => ResourceManager.GetString(nameof(Import_OrderChoiceMessage)) ?? string.Empty;
    public static string Import_NewOrder => ResourceManager.GetString(nameof(Import_NewOrder)) ?? string.Empty;
    public static string Import_ExistingOrder => ResourceManager.GetString(nameof(Import_ExistingOrder)) ?? string.Empty;
    public static string Import_OrderNumberTitle => ResourceManager.GetString(nameof(Import_OrderNumberTitle)) ?? string.Empty;
    public static string Import_OrderNumberMessage => ResourceManager.GetString(nameof(Import_OrderNumberMessage)) ?? string.Empty;
    public static string Import_OrderNumberRequired => ResourceManager.GetString(nameof(Import_OrderNumberRequired)) ?? string.Empty;
    public static string Import_ReplaceWarningTitle => ResourceManager.GetString(nameof(Import_ReplaceWarningTitle)) ?? string.Empty;
    public static string Import_ReplaceWarningMessage => ResourceManager.GetString(nameof(Import_ReplaceWarningMessage)) ?? string.Empty;
}
