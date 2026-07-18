using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private void EnsureAppSettingsFile()
    {
        string? dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(_configFilePath))
        {
            // Check if there is an appsettings.json in the application directory to use as a template
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(templatePath))
            {
                try
                {
                    File.Copy(templatePath, _configFilePath, true);
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"⚠️ Could not copy appsettings.json template: {ex.Message}");
                }
            }

            var defaultSettings = new Models.AppSettingsDto
            {
                Bexio = new Models.BexioSettingsDto(),
                ActiveProfileName = "Default",
                Profiles = new System.Collections.Generic.List<Models.MappingProfileDto>
                {
                    new Models.MappingProfileDto { Name = "Default", ExcelMapping = new Models.ExcelMappingDto() }
                }
            };
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private void LoadSettings()
    {
        try
        {
            EnsureAppSettingsFile();
            string text = File.ReadAllText(_configFilePath);
            var dto = JsonSerializer.Deserialize<Models.AppSettingsDto>(text) ?? new Models.AppSettingsDto();

            var encryptedToken = dto.Bexio.ApiToken;
            BexioToken = _encryptionService.Decrypt(encryptedToken);
            if (string.IsNullOrEmpty(BexioToken) && !string.IsNullOrEmpty(encryptedToken) && encryptedToken != "bexio_api_token_here")
                BexioToken = encryptedToken;

            AccountId = dto.Bexio.AccountId;
            TaxId = dto.Bexio.TaxId;
            SelectedLanguage = dto.Bexio.Language;
            _initialLanguage = SelectedLanguage;
            ApplyLanguage(SelectedLanguage);

            Profiles.Clear();
            if (dto.Profiles != null && dto.Profiles.Count > 0)
            {
                foreach (var p in dto.Profiles)
                {
                    Profiles.Add(new Models.MappingProfile { Name = p.Name, Mapping = MapDtoToOptions(p.ExcelMapping) });
                }
            }
            else
            {
                Profiles.Add(new Models.MappingProfile { Name = "Default", Mapping = new ExcelMappingOptions() });
            }


            var active = Profiles.FirstOrDefault(p => p.Name.Equals(dto.ActiveProfileName, StringComparison.OrdinalIgnoreCase)) ?? Profiles[0];
            _activeProfile = active;
            SelectedProfile = active;
            OnPropertyChanged(nameof(ActiveProfile));
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorDialog($"{Resources.Translations.Settings_ErrorLoad}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
        IsModified = false;
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    private void SaveSettings()
    {
        try
        {
            if (SelectedProfile != null)
            {
                CopyVmToProfile(SelectedProfile);
            }

            string encryptedToken = _encryptionService.Encrypt(BexioToken);

            var settingsObj = new Models.AppSettingsDto
            {
                Bexio = new Models.BexioSettingsDto
                {
                    ApiToken = encryptedToken,
                    AccountId = AccountId,
                    TaxId = TaxId,
                    Language = SelectedLanguage
                },
                ActiveProfileName = ActiveProfile?.Name ?? "Default",
                Profiles = Profiles.Select(p => new Models.MappingProfileDto
                {
                    Name = p.Name,
                    ExcelMapping = MapOptionsToDto(p.Mapping)
                }).ToList()
            };

            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true }));

            _ = CheckBexioConnectionAsync();

            if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }

            ApplyLanguage(SelectedLanguage);
            bool languageChanged = SelectedLanguage != _initialLanguage;

            HandleLanguageReload(languageChanged);

            AppendLog("Settings saved successfully and active Excel file reloaded.");
            IsModified = false;
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorDialog($"{Resources.Translations.Settings_ErrorSave}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
    }

    private void HandleLanguageReload(bool languageChanged)
    {
        if (!languageChanged)
        {
            _dialogService.ShowInfoDialog(Resources.Translations.Dialog_SettingsSaved);
            return;
        }

        bool reload = _dialogService.ShowConfirmDialog(
            Resources.Translations.Settings_ReloadPromptMessage,
            Resources.Translations.Settings_ReloadPromptTitle);

        if (reload)
        {
            InvokeOnUiAsync(() =>
            {
                if (System.Windows.Application.Current is App)
                {
                    var newWindow = new Views.MainWindow();
                    newWindow.Show();
                    System.Windows.Application.Current.MainWindow.Close();
                    System.Windows.Application.Current.MainWindow = newWindow;
                }
            });
        }
        _initialLanguage = SelectedLanguage;
    }

    private void CopyVmToProfile(Models.MappingProfile profile)
    {
        profile.Mapping.Header.CompanyNameCell = CompanyNameCell;
        profile.Mapping.Header.StreetCell = StreetCell;
        profile.Mapping.Header.ZipCityCell = ZipCityCell;
        profile.Mapping.Header.BuyerEmailCell = BuyerEmailCell;
        profile.Mapping.Header.BuyerNameCell = BuyerNameCell;
        profile.Mapping.Header.OrderIdCell = OrderIdCell;
        profile.Mapping.Header.PaymentTermsCell = PaymentTermsCell;
        profile.Mapping.Header.DiscountCell = DiscountCell;

        profile.Mapping.SizeMatrix.StartRow = MatrixStartRow;
        profile.Mapping.SizeMatrix.EndRow = MatrixEndRow;
        profile.Mapping.SizeMatrix.CategoryColumn = MatrixCategoryCol;
        profile.Mapping.SizeMatrix.StartSizeColumn = MatrixStartSizeCol;
        profile.Mapping.SizeMatrix.EndSizeColumn = MatrixEndSizeCol;

        profile.Mapping.Data.StartRow = DataStartRow;
        profile.Mapping.Data.ArticleNumberColumn = ColArtNum;
        profile.Mapping.Data.ArticleNameColumn = ColArtName;
        profile.Mapping.Data.ColorColumn = ColColor;
        profile.Mapping.Data.CategoryColumn = ColSizeCategory;
        profile.Mapping.Data.StartQtyColumn = ColStartQty;
        profile.Mapping.Data.EndQtyColumn = ColEndQty;
        profile.Mapping.Data.UnitPriceColumn = ColUnitPrice;
        profile.Mapping.DefaultOrderName = DefaultOrderName;
        profile.Mapping.SeasonCode = SeasonCode;
        profile.Mapping.PositionTextTemplate = PositionTextTemplate;
    }

    private void CopyProfileToVm(Models.MappingProfile profile)
    {
        CompanyNameCell = profile.Mapping.Header.CompanyNameCell;
        StreetCell = profile.Mapping.Header.StreetCell;
        ZipCityCell = profile.Mapping.Header.ZipCityCell;
        BuyerEmailCell = profile.Mapping.Header.BuyerEmailCell;
        BuyerNameCell = profile.Mapping.Header.BuyerNameCell;
        OrderIdCell = profile.Mapping.Header.OrderIdCell;
        PaymentTermsCell = profile.Mapping.Header.PaymentTermsCell;
        DiscountCell = profile.Mapping.Header.DiscountCell;

        MatrixStartRow = profile.Mapping.SizeMatrix.StartRow;
        MatrixEndRow = profile.Mapping.SizeMatrix.EndRow;
        MatrixCategoryCol = profile.Mapping.SizeMatrix.CategoryColumn;
        MatrixStartSizeCol = profile.Mapping.SizeMatrix.StartSizeColumn;
        MatrixEndSizeCol = profile.Mapping.SizeMatrix.EndSizeColumn;

        DataStartRow = profile.Mapping.Data.StartRow;
        ColArtNum = profile.Mapping.Data.ArticleNumberColumn;
        ColArtName = profile.Mapping.Data.ArticleNameColumn;
        ColColor = profile.Mapping.Data.ColorColumn;
        ColSizeCategory = profile.Mapping.Data.CategoryColumn;
        ColStartQty = profile.Mapping.Data.StartQtyColumn;
        ColEndQty = profile.Mapping.Data.EndQtyColumn;
        ColUnitPrice = profile.Mapping.Data.UnitPriceColumn;
        DefaultOrderName = profile.Mapping.DefaultOrderName;
        SeasonCode = profile.Mapping.SeasonCode;
        PositionTextTemplate = profile.Mapping.PositionTextTemplate;
    }

    private ExcelMappingOptions BuildMappingOptions()
    {
        return ActiveProfile != null ? ActiveProfile.Mapping : new ExcelMappingOptions();
    }

    private void ApplyLanguage(string lang)
    {
        Helpers.LanguageHelper.Apply(lang);
    }

    private ExcelMappingOptions MapDtoToOptions(Models.ExcelMappingDto dto)
    {
        return new ExcelMappingOptions
        {
            WorksheetIndex = dto.WorksheetIndex,
            DefaultOrderName = dto.DefaultOrderName,
            SeasonCode = dto.SeasonCode,
            PositionTextTemplate = dto.PositionTextTemplate,
            Header = new HeaderMapping
            {
                CompanyNameCell = dto.Header.CompanyNameCell,
                StreetCell = dto.Header.StreetCell,
                ZipCityCell = dto.Header.ZipCityCell,
                BuyerEmailCell = dto.Header.BuyerEmailCell,
                BuyerNameCell = dto.Header.BuyerNameCell,
                OrderIdCell = dto.Header.OrderIdCell,
                PaymentTermsCell = dto.Header.PaymentTermsCell,
                DiscountCell = dto.Header.DiscountCell
            },
            SizeMatrix = new SizeMatrixMapping
            {
                StartRow = dto.SizeMatrix.StartRow,
                EndRow = dto.SizeMatrix.EndRow,
                CategoryColumn = dto.SizeMatrix.CategoryColumn,
                StartSizeColumn = dto.SizeMatrix.StartSizeColumn,
                EndSizeColumn = dto.SizeMatrix.EndSizeColumn
            },
            Data = new DataMapping
            {
                StartRow = dto.Data.StartRow,
                ArticleNumberColumn = dto.Data.ArticleNumberColumn,
                ArticleNameColumn = dto.Data.ArticleNameColumn,
                ColorColumn = dto.Data.ColorColumn,
                CategoryColumn = dto.Data.CategoryColumn,
                StartQtyColumn = dto.Data.StartQtyColumn,
                EndQtyColumn = dto.Data.EndQtyColumn,
                UnitPriceColumn = dto.Data.UnitPriceColumn
            }
        };
    }

    private Models.ExcelMappingDto MapOptionsToDto(ExcelMappingOptions opts)
    {
        return new Models.ExcelMappingDto
        {
            WorksheetIndex = opts.WorksheetIndex,
            DefaultOrderName = opts.DefaultOrderName,
            SeasonCode = opts.SeasonCode,
            PositionTextTemplate = opts.PositionTextTemplate,
            Header = new Models.HeaderMappingDto
            {
                CompanyNameCell = opts.Header.CompanyNameCell,
                StreetCell = opts.Header.StreetCell,
                ZipCityCell = opts.Header.ZipCityCell,
                BuyerEmailCell = opts.Header.BuyerEmailCell,
                BuyerNameCell = opts.Header.BuyerNameCell,
                OrderIdCell = opts.Header.OrderIdCell,
                PaymentTermsCell = opts.Header.PaymentTermsCell,
                DiscountCell = opts.Header.DiscountCell
            },
            SizeMatrix = new Models.SizeMatrixDto
            {
                StartRow = opts.SizeMatrix.StartRow,
                EndRow = opts.SizeMatrix.EndRow,
                CategoryColumn = opts.SizeMatrix.CategoryColumn,
                StartSizeColumn = opts.SizeMatrix.StartSizeColumn,
                EndSizeColumn = opts.SizeMatrix.EndSizeColumn
            },
            Data = new Models.DataMappingDto
            {
                StartRow = opts.Data.StartRow,
                ArticleNumberColumn = opts.Data.ArticleNumberColumn,
                ArticleNameColumn = opts.Data.ArticleNameColumn,
                ColorColumn = opts.Data.ColorColumn,
                CategoryColumn = opts.Data.CategoryColumn,
                StartQtyColumn = opts.Data.StartQtyColumn,
                EndQtyColumn = opts.Data.EndQtyColumn,
                UnitPriceColumn = opts.Data.UnitPriceColumn
            }
        };
    }
}
