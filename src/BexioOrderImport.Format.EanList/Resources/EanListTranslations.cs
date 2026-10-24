using System.Resources;

namespace BexioOrderImport.Format.EanList.Resources;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class EanListTranslations
{
    private static readonly ResourceManager ResourceManager =
        new("BexioOrderImport.Format.EanList.Resources.EanListTranslations", typeof(EanListTranslations).Assembly);

    public static string Format_DisplayName => ResourceManager.GetString(nameof(Format_DisplayName)) ?? string.Empty;
    public static string Format_Description => ResourceManager.GetString(nameof(Format_Description)) ?? string.Empty;
    public static string Settings_SourceSection => ResourceManager.GetString(nameof(Settings_SourceSection)) ?? string.Empty;
    public static string Settings_WorksheetLabel => ResourceManager.GetString(nameof(Settings_WorksheetLabel)) ?? string.Empty;
    public static string Settings_StartRowLabel => ResourceManager.GetString(nameof(Settings_StartRowLabel)) ?? string.Empty;
    public static string Settings_StartRowHint => ResourceManager.GetString(nameof(Settings_StartRowHint)) ?? string.Empty;
    public static string Settings_EanColumn => ResourceManager.GetString(nameof(Settings_EanColumn)) ?? string.Empty;
    public static string Settings_QuantityColumn => ResourceManager.GetString(nameof(Settings_QuantityColumn)) ?? string.Empty;
    public static string Settings_UseSalesUnitColumn => ResourceManager.GetString(nameof(Settings_UseSalesUnitColumn)) ?? string.Empty;
    public static string Settings_SalesUnitColumn => ResourceManager.GetString(nameof(Settings_SalesUnitColumn)) ?? string.Empty;
    public static string Settings_CustomerNumberLabel => ResourceManager.GetString(nameof(Settings_CustomerNumberLabel)) ?? string.Empty;
    public static string Settings_CustomerNumberHint => ResourceManager.GetString(nameof(Settings_CustomerNumberHint)) ?? string.Empty;
    public static string Settings_NumbersHint => ResourceManager.GetString(nameof(Settings_NumbersHint)) ?? string.Empty;
    public static string Settings_TextTemplatesSection => ResourceManager.GetString(nameof(Settings_TextTemplatesSection)) ?? string.Empty;
    public static string Settings_DefaultOrderNameLabel => ResourceManager.GetString(nameof(Settings_DefaultOrderNameLabel)) ?? string.Empty;
    public static string Settings_DefaultOrderNameHint => ResourceManager.GetString(nameof(Settings_DefaultOrderNameHint)) ?? string.Empty;
    public static string Settings_PositionTextTemplateLabel => ResourceManager.GetString(nameof(Settings_PositionTextTemplateLabel)) ?? string.Empty;
    public static string Settings_PositionTextTemplateHint => ResourceManager.GetString(nameof(Settings_PositionTextTemplateHint)) ?? string.Empty;
    public static string Validation_PositiveNumber => ResourceManager.GetString(nameof(Validation_PositiveNumber)) ?? string.Empty;
    public static string Validation_InvalidColumn => ResourceManager.GetString(nameof(Validation_InvalidColumn)) ?? string.Empty;
    public static string Validation_Required => ResourceManager.GetString(nameof(Validation_Required)) ?? string.Empty;
    public static string Error_InvalidEan => ResourceManager.GetString(nameof(Error_InvalidEan)) ?? string.Empty;
    public static string Error_MissingEan => ResourceManager.GetString(nameof(Error_MissingEan)) ?? string.Empty;
    public static string Error_InvalidQuantity => ResourceManager.GetString(nameof(Error_InvalidQuantity)) ?? string.Empty;
    public static string Error_InvalidSalesUnit => ResourceManager.GetString(nameof(Error_InvalidSalesUnit)) ?? string.Empty;
    public static string Preview_Header => ResourceManager.GetString(nameof(Preview_Header)) ?? string.Empty;
    public static string Preview_CustomerNumber => ResourceManager.GetString(nameof(Preview_CustomerNumber)) ?? string.Empty;
    public static string Preview_CustomerNumberMissing => ResourceManager.GetString(nameof(Preview_CustomerNumberMissing)) ?? string.Empty;
    public static string Preview_PositionsHeader => ResourceManager.GetString(nameof(Preview_PositionsHeader)) ?? string.Empty;
    public static string Preview_ColRow => ResourceManager.GetString(nameof(Preview_ColRow)) ?? string.Empty;
    public static string Preview_ColEan => ResourceManager.GetString(nameof(Preview_ColEan)) ?? string.Empty;
    public static string Preview_ColQuantity => ResourceManager.GetString(nameof(Preview_ColQuantity)) ?? string.Empty;
    public static string Preview_ColSalesUnit => ResourceManager.GetString(nameof(Preview_ColSalesUnit)) ?? string.Empty;
    public static string Preview_Positions => ResourceManager.GetString(nameof(Preview_Positions)) ?? string.Empty;
    public static string Preview_TotalQuantity => ResourceManager.GetString(nameof(Preview_TotalQuantity)) ?? string.Empty;
    public static string Preview_SkippedRows => ResourceManager.GetString(nameof(Preview_SkippedRows)) ?? string.Empty;
    public static string Preview_PricesHint => ResourceManager.GetString(nameof(Preview_PricesHint)) ?? string.Empty;
}
