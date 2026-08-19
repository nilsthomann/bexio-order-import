using System;
using System.Windows;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Views;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class ProfileEditWindow : Window
{
    private readonly MappingProfile _profile;
    private readonly System.Collections.Generic.IEnumerable<MappingProfile>? _existingProfiles;

    public ProfileEditWindow(MappingProfile profile, System.Collections.Generic.IEnumerable<MappingProfile>? existingProfiles = null)
    {
        InitializeComponent();
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
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
        WorksheetIndexInput.Text = _profile.Mapping.WorksheetIndex.ToString();

        CompanyNameCellInput.Text = _profile.Mapping.Header.CompanyNameCell;
        StreetCellInput.Text = _profile.Mapping.Header.StreetCell;
        ZipCityCellInput.Text = _profile.Mapping.Header.ZipCityCell;
        BuyerEmailCellInput.Text = _profile.Mapping.Header.BuyerEmailCell;
        BuyerNameCellInput.Text = _profile.Mapping.Header.BuyerNameCell;
        EnableOrderNumberCheckBox.IsChecked = _profile.Mapping.Header.EnableOrderNumber;
        OrderNumberCellInput.Text = _profile.Mapping.Header.OrderNumberCell;
        EnableCustomerNumberCheckBox.IsChecked = _profile.Mapping.Header.EnableCustomerNumber;
        CustomerNumberCellInput.Text = _profile.Mapping.Header.CustomerNumberCell;
        PaymentTermsCellInput.Text = _profile.Mapping.Header.PaymentTermsCell;
        DiscountCellInput.Text = _profile.Mapping.Header.DiscountCell;

        MatrixStartRowInput.Text = _profile.Mapping.SizeMatrix.StartRow.ToString();
        MatrixEndRowInput.Text = _profile.Mapping.SizeMatrix.EndRow.ToString();
        MatrixCategoryColInput.Text = _profile.Mapping.SizeMatrix.CategoryColumn;
        MatrixStartSizeColInput.Text = _profile.Mapping.SizeMatrix.StartSizeColumn;
        MatrixEndSizeColInput.Text = _profile.Mapping.SizeMatrix.EndSizeColumn;

        DataStartRowInput.Text = _profile.Mapping.Data.StartRow.ToString();
        ColArtNumInput.Text = _profile.Mapping.Data.ArticleNumberColumn;
        ColArtNameInput.Text = _profile.Mapping.Data.ArticleNameColumn;
        ColColorInput.Text = _profile.Mapping.Data.ColorColumn;
        ColSizeCategoryInput.Text = _profile.Mapping.Data.CategoryColumn;
        ColStartQtyInput.Text = _profile.Mapping.Data.StartQtyColumn;
        ColEndQtyInput.Text = _profile.Mapping.Data.EndQtyColumn;
        ColUnitPriceInput.Text = _profile.Mapping.Data.UnitPriceColumn;
        EnableRowDiscountCheckBox.IsChecked = _profile.Mapping.Data.EnableRowDiscount;
        ColRowDiscountInput.Text = _profile.Mapping.Data.RowDiscountColumn;
        DefaultOrderNameInput.Text = _profile.Mapping.DefaultOrderName;
        SeasonCodeInput.Text = _profile.Mapping.SeasonCode;
        SinglePositionTextTemplateInput.Text = _profile.Mapping.SinglePositionTextTemplate;
        GroupedPositionTextTemplateInput.Text = _profile.Mapping.GroupedPositionTextTemplate;
        DiscountPositionTextTemplateInput.Text = _profile.Mapping.DiscountPositionTextTemplate;
        SizeRowTemplateInput.Text = string.IsNullOrWhiteSpace(_profile.Mapping.SizeRowTemplate) ? "{Amount}x Size {Size}" : _profile.Mapping.SizeRowTemplate;

        if (_profile.Mapping.PositionGroupingMode == Domain.Models.PositionGroupingMode.GroupedSizePosition)
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
                ? Domain.Models.PositionGroupingMode.GroupedSizePosition
                : Domain.Models.PositionGroupingMode.SinglePositionPerSize;

            string singleTemplate = SinglePositionTextTemplateInput.Text.Trim();
            string groupedTemplate = GroupedPositionTextTemplateInput.Text.Trim();

            if (selectedGroupingMode == Domain.Models.PositionGroupingMode.GroupedSizePosition && !groupedTemplate.Contains("{SizesRows}"))
            {
                Views.CustomDialog.ShowWarning(Translations.Dialog_SizesRowsPlaceholderRequiredForGrouped);
                return;
            }

            _profile.Name = newName;

            // Parse and Validate inputs
            _profile.Mapping.WorksheetIndex = int.Parse(WorksheetIndexInput.Text.Trim());

            _profile.Mapping.Header.CompanyNameCell = CompanyNameCellInput.Text.Trim();
            _profile.Mapping.Header.StreetCell = StreetCellInput.Text.Trim();
            _profile.Mapping.Header.ZipCityCell = ZipCityCellInput.Text.Trim();
            _profile.Mapping.Header.BuyerEmailCell = BuyerEmailCellInput.Text.Trim();
            _profile.Mapping.Header.BuyerNameCell = BuyerNameCellInput.Text.Trim();
            _profile.Mapping.Header.EnableOrderNumber = EnableOrderNumberCheckBox.IsChecked == true;
            _profile.Mapping.Header.OrderNumberCell = OrderNumberCellInput.Text.Trim();
            _profile.Mapping.Header.EnableCustomerNumber = EnableCustomerNumberCheckBox.IsChecked == true;
            _profile.Mapping.Header.CustomerNumberCell = CustomerNumberCellInput.Text.Trim();
            _profile.Mapping.Header.PaymentTermsCell = PaymentTermsCellInput.Text.Trim();
            _profile.Mapping.Header.DiscountCell = DiscountCellInput.Text.Trim();

            _profile.Mapping.SizeMatrix.StartRow = int.Parse(MatrixStartRowInput.Text.Trim());
            _profile.Mapping.SizeMatrix.EndRow = int.Parse(MatrixEndRowInput.Text.Trim());
            _profile.Mapping.SizeMatrix.CategoryColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(MatrixCategoryColInput.Text, "D");
            _profile.Mapping.SizeMatrix.StartSizeColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(MatrixStartSizeColInput.Text, "E");
            _profile.Mapping.SizeMatrix.EndSizeColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(MatrixEndSizeColInput.Text, "R");

            _profile.Mapping.Data.StartRow = int.Parse(DataStartRowInput.Text.Trim());
            _profile.Mapping.Data.ArticleNumberColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColArtNumInput.Text, "A");
            _profile.Mapping.Data.ArticleNameColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColArtNameInput.Text, "B");
            _profile.Mapping.Data.ColorColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColColorInput.Text, "C");
            _profile.Mapping.Data.CategoryColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColSizeCategoryInput.Text, "D");
            _profile.Mapping.Data.StartQtyColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColStartQtyInput.Text, "E");
            _profile.Mapping.Data.EndQtyColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColEndQtyInput.Text, "R");
            _profile.Mapping.Data.UnitPriceColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColUnitPriceInput.Text, "T");
            _profile.Mapping.Data.EnableRowDiscount = EnableRowDiscountCheckBox.IsChecked == true;
            _profile.Mapping.Data.RowDiscountColumn = Application.Helpers.ExcelColumnHelper.NormalizeColumnLetter(ColRowDiscountInput.Text, "U");
            _profile.Mapping.DefaultOrderName = DefaultOrderNameInput.Text.Trim();
            _profile.Mapping.SeasonCode = SeasonCodeInput.Text.Trim();
            _profile.Mapping.PositionGroupingMode = selectedGroupingMode;
            _profile.Mapping.SinglePositionTextTemplate = singleTemplate;
            _profile.Mapping.GroupedPositionTextTemplate = groupedTemplate;
            _profile.Mapping.SizeRowTemplate = SizeRowTemplateInput.Text.Trim();
            _profile.Mapping.DiscountPositionTextTemplate = DiscountPositionTextTemplateInput.Text.Trim();

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
