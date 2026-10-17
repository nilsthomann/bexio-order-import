using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Wpf.ViewModels;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class BexioOptionListsTests
{
    private readonly Mock<IBexioService> _bexio = new();
    private readonly List<string> _logs = [];

    [Test]
    public async Task LoadAsync_ShouldReplaceTheListsWithTheBexioOptions()
    {
        var lists = new BexioOptionLists();
        lists.Accounts.Add(new BexioAccount { Id = 1 });
        _bexio.Setup(b => b.GetAccountsAsync()).ReturnsAsync([new BexioAccount { Id = 100 }, new BexioAccount { Id = 101 }]);
        _bexio.Setup(b => b.GetTaxesAsync()).ReturnsAsync([new BexioTax { Id = 5 }]);

        await lists.LoadAsync(_bexio.Object, 100, 5, _logs.Add);

        lists.Accounts.Select(a => a.Id).Should().Equal(100, 101);
        lists.Taxes.Select(t => t.Id).Should().Equal(5);
        _logs.Should().BeEmpty();
    }

    [Test]
    public async Task LoadAsync_WhenBexioHasNoOptionsAndNothingIsSelected_ShouldAddAnEmptyEntry()
    {
        var lists = new BexioOptionLists();
        _bexio.Setup(b => b.GetAccountsAsync()).ReturnsAsync([]);
        _bexio.Setup(b => b.GetTaxesAsync()).ReturnsAsync([]);

        await lists.LoadAsync(_bexio.Object, null, null, _logs.Add);

        lists.Accounts.Should().ContainSingle(a => a.Id == 0 && a.AccountNo == string.Empty);
        lists.Taxes.Should().ContainSingle(t => t.Id == 0 && t.DisplayName == string.Empty);
    }

    [Test]
    public async Task LoadAsync_WhenLoadingFails_ShouldLogAndKeepTheSelectedIds()
    {
        var lists = new BexioOptionLists();
        _bexio.Setup(b => b.GetAccountsAsync()).ThrowsAsync(new HttpRequestException("accounts down"));
        _bexio.Setup(b => b.GetTaxesAsync()).ThrowsAsync(new HttpRequestException("taxes down"));

        await lists.LoadAsync(_bexio.Object, 100, 5, _logs.Add);

        lists.Accounts.Should().ContainSingle(a => a.Id == 100 && a.AccountNo == "100");
        lists.Taxes.Should().ContainSingle(t => t.Id == 5 && t.DisplayName == "5");
        _logs.Should().Equal("⚠️ Could not load accounts: accounts down", "⚠️ Could not load tax rates: taxes down");
    }

    [Test]
    public async Task LoadAsync_WhenLoadingFailsWithoutSelection_ShouldAddAnEmptyEntry()
    {
        var lists = new BexioOptionLists();
        _bexio.Setup(b => b.GetAccountsAsync()).ThrowsAsync(new HttpRequestException("down"));
        _bexio.Setup(b => b.GetTaxesAsync()).ThrowsAsync(new HttpRequestException("down"));

        await lists.LoadAsync(_bexio.Object, null, null, _logs.Add);

        lists.Accounts.Should().ContainSingle(a => a.Id == 0);
        lists.Taxes.Should().ContainSingle(t => t.Id == 0);
    }

    [Test]
    public void KeepSelected_ShouldKeepOnlyTheSelectedOptionsOrAPlaceholder()
    {
        var lists = new BexioOptionLists();
        var selectedAccount = new BexioAccount { Id = 100, Name = "Sales" };
        lists.Accounts.Add(selectedAccount);
        lists.Accounts.Add(new BexioAccount { Id = 101 });
        lists.Taxes.Add(new BexioTax { Id = 6 });

        lists.KeepSelected(100, 5);

        lists.Accounts.Should().ContainSingle().Which.Should().BeSameAs(selectedAccount);
        lists.Taxes.Should().ContainSingle(t => t.Id == 5 && t.DisplayName == "5");
    }

    [Test]
    public void KeepSelected_WithoutSelection_ShouldClearTheLists()
    {
        var lists = new BexioOptionLists();
        lists.Accounts.Add(new BexioAccount { Id = 100 });
        lists.Taxes.Add(new BexioTax { Id = 5 });

        lists.KeepSelected(null, null);

        lists.Accounts.Should().BeEmpty();
        lists.Taxes.Should().BeEmpty();
    }
}
