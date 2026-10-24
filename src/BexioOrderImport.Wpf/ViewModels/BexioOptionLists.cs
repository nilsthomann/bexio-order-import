using System.Collections.ObjectModel;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Wpf.ViewModels;

/// <summary>
/// The Bexio accounts, tax rates and payment types offered in the settings. If they cannot be loaded, the lists keep
/// an entry for the selected id, so the saved selection stays visible.
/// </summary>
public sealed class BexioOptionLists
{
    public ObservableCollection<BexioAccount> Accounts { get; } = [];
    public ObservableCollection<BexioTax> Taxes { get; } = [];
    public ObservableCollection<BexioPaymentType> PaymentTypes { get; } = [];

    /// <summary>
    /// Replaces the lists with the accounts, tax rates and payment types of Bexio. The caller restores the selection afterwards,
    /// because replacing the items clears it in the bound combo boxes.
    /// </summary>
    public async Task LoadAsync(IBexioService client, int? accountId, int? taxId, int? paymentTypeId, Action<string> log)
    {
        try
        {
            Replace(Accounts, await client.GetAccountsAsync());
            if (Accounts.Count == 0 && !accountId.HasValue)
            {
                Accounts.Add(EmptyAccount());
            }
        }
        catch (Exception ex)
        {
            log($"⚠️ Could not load accounts: {ex.Message}");
            if (Accounts.Count == 0)
            {
                Accounts.Add(accountId.HasValue ? AccountPlaceholder(accountId.Value) : EmptyAccount());
            }
        }

        try
        {
            Replace(Taxes, await client.GetTaxesAsync());
            if (Taxes.Count == 0 && !taxId.HasValue)
            {
                Taxes.Add(EmptyTax());
            }
        }
        catch (Exception ex)
        {
            log($"⚠️ Could not load tax rates: {ex.Message}");
            if (Taxes.Count == 0)
            {
                Taxes.Add(taxId.HasValue ? TaxPlaceholder(taxId.Value) : EmptyTax());
            }
        }

        try
        {
            Replace(PaymentTypes, await client.GetPaymentTypesAsync());
            if (PaymentTypes.Count == 0 && !paymentTypeId.HasValue)
            {
                PaymentTypes.Add(EmptyPaymentType());
            }
        }
        catch (Exception ex)
        {
            log($"⚠️ Could not load payment types: {ex.Message}");
            if (PaymentTypes.Count == 0)
            {
                PaymentTypes.Add(paymentTypeId.HasValue ? PaymentTypePlaceholder(paymentTypeId.Value) : EmptyPaymentType());
            }
        }
    }

    /// <summary>
    /// Without a connection only the selected account, tax rate and payment type remain.
    /// </summary>
    public void KeepSelected(int? accountId, int? taxId, int? paymentTypeId)
    {
        var selectedAccount = Accounts.FirstOrDefault(x => x.Id == accountId);
        Accounts.Clear();
        if (accountId.HasValue)
        {
            Accounts.Add(selectedAccount ?? AccountPlaceholder(accountId.Value));
        }

        var selectedTax = Taxes.FirstOrDefault(x => x.Id == taxId);
        Taxes.Clear();
        if (taxId.HasValue)
        {
            Taxes.Add(selectedTax ?? TaxPlaceholder(taxId.Value));
        }

        var selectedPaymentType = PaymentTypes.FirstOrDefault(x => x.Id == paymentTypeId);
        PaymentTypes.Clear();
        if (paymentTypeId.HasValue)
        {
            PaymentTypes.Add(selectedPaymentType ?? PaymentTypePlaceholder(paymentTypeId.Value));
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static BexioAccount EmptyAccount() => new() { AccountNo = string.Empty, Name = string.Empty };

    private static BexioAccount AccountPlaceholder(int id) => new() { Id = id, AccountNo = id.ToString(), Name = string.Empty };

    private static BexioTax EmptyTax() => new() { DisplayName = string.Empty };

    private static BexioTax TaxPlaceholder(int id) => new() { Id = id, DisplayName = id.ToString() };

    private static BexioPaymentType EmptyPaymentType() => new();

    private static BexioPaymentType PaymentTypePlaceholder(int id) => new() { Id = id, Name = id.ToString() };
}
