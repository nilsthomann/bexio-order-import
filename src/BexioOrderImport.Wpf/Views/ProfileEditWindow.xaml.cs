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
        EnableOrderIdCheckBox.IsChecked = _profile.Mapping.Header.EnableOrderId;
        OrderIdCellInput.Text = _profile.Mapping.Header.OrderIdCell;
        EnableCustomerIdCheckBox.IsChecked = _profile.Mapping.Header.EnableCustomerId;
        CustomerIdCellInput.Text = _profile.Mapping.Header.CustomerIdCell;
        PaymentTermsCellInput.Text = _profile.Mapping.Header.PaymentTermsCell;
        DiscountCellInput.Text = _profile.Mapping.Header.DiscountCell;

        MatrixStartRowInput.Text = _profile.Mapping.SizeMatrix.StartRow.ToString();
        MatrixEndRowInput.Text = _profile.Mapping.SizeMatrix.EndRow.ToString();
        MatrixCategoryColInput.Text = _profile.Mapping.SizeMatrix.CategoryColumn.ToString();
        MatrixStartSizeColInput.Text = _profile.Mapping.SizeMatrix.StartSizeColumn.ToString();
        MatrixEndSizeColInput.Text = _profile.Mapping.SizeMatrix.EndSizeColumn.ToString();

        DataStartRowInput.Text = _profile.Mapping.Data.StartRow.ToString();
        ColArtNumInput.Text = _profile.Mapping.Data.ArticleNumberColumn.ToString();
        ColArtNameInput.Text = _profile.Mapping.Data.ArticleNameColumn.ToString();
        ColColorInput.Text = _profile.Mapping.Data.ColorColumn.ToString();
        ColSizeCategoryInput.Text = _profile.Mapping.Data.CategoryColumn.ToString();
        ColStartQtyInput.Text = _profile.Mapping.Data.StartQtyColumn.ToString();
        ColEndQtyInput.Text = _profile.Mapping.Data.EndQtyColumn.ToString();
        ColUnitPriceInput.Text = _profile.Mapping.Data.UnitPriceColumn.ToString();
        EnableRowDiscountCheckBox.IsChecked = _profile.Mapping.Data.EnableRowDiscount;
        ColRowDiscountInput.Text = _profile.Mapping.Data.RowDiscountColumn.ToString();
        DefaultOrderNameInput.Text = _profile.Mapping.DefaultOrderName;
        SeasonCodeInput.Text = _profile.Mapping.SeasonCode;
        PositionTextTemplateInput.Text = _profile.Mapping.PositionTextTemplate;
        DiscountPositionTextTemplateInput.Text = _profile.Mapping.DiscountPositionTextTemplate;
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

            _profile.Name = newName;

            // Parse and Validate inputs
            _profile.Mapping.WorksheetIndex = int.Parse(WorksheetIndexInput.Text.Trim());

            _profile.Mapping.Header.CompanyNameCell = CompanyNameCellInput.Text.Trim();
            _profile.Mapping.Header.StreetCell = StreetCellInput.Text.Trim();
            _profile.Mapping.Header.ZipCityCell = ZipCityCellInput.Text.Trim();
            _profile.Mapping.Header.BuyerEmailCell = BuyerEmailCellInput.Text.Trim();
            _profile.Mapping.Header.BuyerNameCell = BuyerNameCellInput.Text.Trim();
            _profile.Mapping.Header.EnableOrderId = EnableOrderIdCheckBox.IsChecked == true;
            _profile.Mapping.Header.OrderIdCell = OrderIdCellInput.Text.Trim();
            _profile.Mapping.Header.EnableCustomerId = EnableCustomerIdCheckBox.IsChecked == true;
            _profile.Mapping.Header.CustomerIdCell = CustomerIdCellInput.Text.Trim();
            _profile.Mapping.Header.PaymentTermsCell = PaymentTermsCellInput.Text.Trim();
            _profile.Mapping.Header.DiscountCell = DiscountCellInput.Text.Trim();

            _profile.Mapping.SizeMatrix.StartRow = int.Parse(MatrixStartRowInput.Text.Trim());
            _profile.Mapping.SizeMatrix.EndRow = int.Parse(MatrixEndRowInput.Text.Trim());
            _profile.Mapping.SizeMatrix.CategoryColumn = int.Parse(MatrixCategoryColInput.Text.Trim());
            _profile.Mapping.SizeMatrix.StartSizeColumn = int.Parse(MatrixStartSizeColInput.Text.Trim());
            _profile.Mapping.SizeMatrix.EndSizeColumn = int.Parse(MatrixEndSizeColInput.Text.Trim());

            _profile.Mapping.Data.StartRow = int.Parse(DataStartRowInput.Text.Trim());
            _profile.Mapping.Data.ArticleNumberColumn = int.Parse(ColArtNumInput.Text.Trim());
            _profile.Mapping.Data.ArticleNameColumn = int.Parse(ColArtNameInput.Text.Trim());
            _profile.Mapping.Data.ColorColumn = int.Parse(ColColorInput.Text.Trim());
            _profile.Mapping.Data.CategoryColumn = int.Parse(ColSizeCategoryInput.Text.Trim());
            _profile.Mapping.Data.StartQtyColumn = int.Parse(ColStartQtyInput.Text.Trim());
            _profile.Mapping.Data.EndQtyColumn = int.Parse(ColEndQtyInput.Text.Trim());
            _profile.Mapping.Data.UnitPriceColumn = int.Parse(ColUnitPriceInput.Text.Trim());
            _profile.Mapping.Data.EnableRowDiscount = EnableRowDiscountCheckBox.IsChecked == true;
            _profile.Mapping.Data.RowDiscountColumn = int.Parse(ColRowDiscountInput.Text.Trim());
            _profile.Mapping.DefaultOrderName = DefaultOrderNameInput.Text.Trim();
            _profile.Mapping.SeasonCode = SeasonCodeInput.Text.Trim();
            _profile.Mapping.PositionTextTemplate = PositionTextTemplateInput.Text.Trim();
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
