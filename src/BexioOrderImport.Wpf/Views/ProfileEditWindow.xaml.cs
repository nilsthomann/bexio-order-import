using System;
using System.Windows;
using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Views;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class ProfileEditWindow : Window
{
    private readonly MappingProfile _profile;
    private readonly PeakmileSettings _settings;
    private readonly System.Collections.Generic.IEnumerable<MappingProfile>? _existingProfiles;

    public ProfileEditWindow(MappingProfile profile, System.Collections.Generic.IEnumerable<MappingProfile>? existingProfiles = null)
    {
        InitializeComponent();
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _settings = profile.Settings as PeakmileSettings
            ?? throw new ArgumentException($"Profile '{profile.Name}' has no Peakmile settings.", nameof(profile));
        _existingProfiles = existingProfiles;

        Title = $"{Translations.Settings_ProfilesEditTitle}: {profile.Name}";
        TitleTextBlock.Text = $"{Translations.Settings_ProfilesEditTitle}: {profile.Name}";

        LoadProfileData();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SyncToOwner();
        if (Owner != null)
        {
            Owner.LocationChanged += (_, _) => SyncToOwner();
            Owner.SizeChanged    += (_, _) => SyncToOwner();
        }
    }

    private void SyncToOwner()
    {
        if (Owner != null)
        {
            var rect = Helpers.WindowHelper.GetAbsolutePlacement(Owner);
            Left   = rect.Left;
            Top    = rect.Top;
            Width  = rect.Width;
            Height = rect.Height;
        }
        else
        {
            Width  = System.Windows.SystemParameters.PrimaryScreenWidth;
            Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            Left   = 0;
            Top    = 0;
        }
    }

    private void LoadProfileData()
    {
        ProfileNameInput.Text = _profile.Name;
        WorksheetIndexInput.Text = _settings.WorksheetIndex.ToString();

        CompanyNameCellInput.Text = _settings.Header.CompanyNameCell;
        StreetCellInput.Text = _settings.Header.StreetCell;
        ZipCityCellInput.Text = _settings.Header.ZipCityCell;
        BuyerEmailCellInput.Text = _settings.Header.BuyerEmailCell;
        BuyerNameCellInput.Text = _settings.Header.BuyerNameCell;
        EnableOrderNumberCheckBox.IsChecked = _settings.Header.EnableOrderNumber;
        OrderNumberCellInput.Text = _settings.Header.OrderNumberCell;
        EnableCustomerNumberCheckBox.IsChecked = _settings.Header.EnableCustomerNumber;
        CustomerNumberCellInput.Text = _settings.Header.CustomerNumberCell;
        PaymentTermsCellInput.Text = _settings.Header.PaymentTermsCell;
        DiscountCellInput.Text = _settings.Header.DiscountCell;

        MatrixStartRowInput.Text = _settings.SizeMatrix.StartRow.ToString();
        MatrixEndRowInput.Text = _settings.SizeMatrix.EndRow.ToString();
        MatrixCategoryColInput.Text = _settings.SizeMatrix.CategoryColumn;
        MatrixStartSizeColInput.Text = _settings.SizeMatrix.StartSizeColumn;
        MatrixEndSizeColInput.Text = _settings.SizeMatrix.EndSizeColumn;

        DataStartRowInput.Text = _settings.Data.StartRow.ToString();
        ColArtNumInput.Text = _settings.Data.ArticleNumberColumn;
        ColArtNameInput.Text = _settings.Data.ArticleNameColumn;
        ColColorInput.Text = _settings.Data.ColorColumn;
        ColSizeCategoryInput.Text = _settings.Data.CategoryColumn;
        ColStartQtyInput.Text = _settings.Data.StartQtyColumn;
        ColEndQtyInput.Text = _settings.Data.EndQtyColumn;
        ColUnitPriceInput.Text = _settings.Data.UnitPriceColumn;
        EnableRowDiscountCheckBox.IsChecked = _settings.Data.EnableRowDiscount;
        ColRowDiscountInput.Text = _settings.Data.RowDiscountColumn;
        DefaultOrderNameInput.Text = _settings.DefaultOrderName;
        SeasonCodeInput.Text = _settings.SeasonCode;
        SeasonFallbackCodesInput.Text = string.Join(", ", _settings.SeasonFallbackCodes);
        SinglePositionTextTemplateInput.Text = _settings.SinglePositionTextTemplate;
        GroupedPositionTextTemplateInput.Text = _settings.GroupedPositionTextTemplate;
        DiscountPositionTextTemplateInput.Text = _settings.DiscountPositionTextTemplate;
        SizeRowTemplateInput.Text = string.IsNullOrWhiteSpace(_settings.SizeRowTemplate) ? "{Amount}x Size {Size}" : _settings.SizeRowTemplate;

        if (_settings.PositionGroupingMode == PositionGroupingMode.GroupedSizePosition)
        {
            GroupedPositionRadioButton.IsChecked = true;
        }
        else
        {
            SinglePositionRadioButton.IsChecked = true;
        }

        UpdateTemplateVisibility();
    }

    private void PositionGroupingRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        UpdateTemplateVisibility();
    }

    private void UpdateTemplateVisibility()
    {
        if (SinglePositionTemplatePanel == null || GroupedPositionTemplatePanel == null) return;
        bool isGrouped = GroupedPositionRadioButton.IsChecked == true;
        SinglePositionTemplatePanel.Visibility = isGrouped ? Visibility.Collapsed : Visibility.Visible;
        GroupedPositionTemplatePanel.Visibility = isGrouped ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string newName = ProfileNameInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                Views.CustomDialog.ShowWarning(Translations.Dialog_ProfileNameRequired);
                return;
            }

            if (!newName.Equals(_profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (_existingProfiles != null && System.Linq.Enumerable.Any(_existingProfiles, p => p != _profile && p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    Views.CustomDialog.ShowError(Translations.Dialog_ProfileNameExists, Translations.Dialog_ErrorTitle);
                    return;
                }
            }

            var selectedGroupingMode = GroupedPositionRadioButton.IsChecked == true
                ? PositionGroupingMode.GroupedSizePosition
                : PositionGroupingMode.SinglePositionPerSize;

            string singleTemplate = SinglePositionTextTemplateInput.Text.Trim();
            string groupedTemplate = GroupedPositionTextTemplateInput.Text.Trim();

            if (selectedGroupingMode == PositionGroupingMode.GroupedSizePosition && !groupedTemplate.Contains("{SizesRows}"))
            {
                Views.CustomDialog.ShowWarning(Translations.Dialog_SizesRowsPlaceholderRequiredForGrouped);
                return;
            }

            _profile.Name = newName;

            // Parse and Validate inputs
            _settings.WorksheetIndex = int.Parse(WorksheetIndexInput.Text.Trim());

            _settings.Header.CompanyNameCell = CompanyNameCellInput.Text.Trim();
            _settings.Header.StreetCell = StreetCellInput.Text.Trim();
            _settings.Header.ZipCityCell = ZipCityCellInput.Text.Trim();
            _settings.Header.BuyerEmailCell = BuyerEmailCellInput.Text.Trim();
            _settings.Header.BuyerNameCell = BuyerNameCellInput.Text.Trim();
            _settings.Header.EnableOrderNumber = EnableOrderNumberCheckBox.IsChecked == true;
            _settings.Header.OrderNumberCell = OrderNumberCellInput.Text.Trim();
            _settings.Header.EnableCustomerNumber = EnableCustomerNumberCheckBox.IsChecked == true;
            _settings.Header.CustomerNumberCell = CustomerNumberCellInput.Text.Trim();
            _settings.Header.PaymentTermsCell = PaymentTermsCellInput.Text.Trim();
            _settings.Header.DiscountCell = DiscountCellInput.Text.Trim();

            _settings.SizeMatrix.StartRow = int.Parse(MatrixStartRowInput.Text.Trim());
            _settings.SizeMatrix.EndRow = int.Parse(MatrixEndRowInput.Text.Trim());
            _settings.SizeMatrix.CategoryColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(MatrixCategoryColInput.Text, "D");
            _settings.SizeMatrix.StartSizeColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(MatrixStartSizeColInput.Text, "E");
            _settings.SizeMatrix.EndSizeColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(MatrixEndSizeColInput.Text, "R");

            _settings.Data.StartRow = int.Parse(DataStartRowInput.Text.Trim());
            _settings.Data.ArticleNumberColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColArtNumInput.Text, "A");
            _settings.Data.ArticleNameColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColArtNameInput.Text, "B");
            _settings.Data.ColorColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColColorInput.Text, "C");
            _settings.Data.CategoryColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColSizeCategoryInput.Text, "D");
            _settings.Data.StartQtyColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColStartQtyInput.Text, "E");
            _settings.Data.EndQtyColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColEndQtyInput.Text, "R");
            _settings.Data.UnitPriceColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColUnitPriceInput.Text, "T");
            _settings.Data.EnableRowDiscount = EnableRowDiscountCheckBox.IsChecked == true;
            _settings.Data.RowDiscountColumn = Abstractions.Excel.ExcelColumnHelper.NormalizeColumnLetter(ColRowDiscountInput.Text, "U");
            _settings.DefaultOrderName = DefaultOrderNameInput.Text.Trim();
            _settings.SeasonCode = SeasonCodeInput.Text.Trim();
            _settings.SeasonFallbackCodes = [.. SeasonFallbackCodesInput.Text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            _settings.PositionGroupingMode = selectedGroupingMode;
            _settings.SinglePositionTextTemplate = singleTemplate;
            _settings.GroupedPositionTextTemplate = groupedTemplate;
            _settings.SizeRowTemplate = SizeRowTemplateInput.Text.Trim();
            _settings.DiscountPositionTextTemplate = DiscountPositionTextTemplateInput.Text.Trim();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Views.CustomDialog.ShowError($"{Translations.Settings_ErrorSave}: {ex.Message}", Translations.Settings_ErrorTitle);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
