using BexioOrderImport.Abstractions.Import;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class DelegateImportUserInteractionServiceTests
{
    [Test]
    public async Task WithoutDelegates_InputsShouldCancelAndWarningsShouldBeDeclined()
    {
        var interaction = new DelegateImportUserInteractionService();

        (await interaction.AskTextAsync("Title", "Message")).Should().BeNull();
        (await interaction.ChooseAsync("Title", "Message", ["A", "B"])).Should().BeNull();
        (await interaction.ConfirmWarningAsync("Title", "Message")).Should().BeFalse();
        interaction.OrderPositionsDeleted();
    }

    [Test]
    public async Task WithDelegates_ShouldPassTheArgumentsAndReturnTheAnswers()
    {
        var calls = new List<string>();
        var interaction = new DelegateImportUserInteractionService(
            askText: (title, message, error) => { calls.Add($"{title}|{message}|{error}"); return Task.FromResult<string?>("1001"); },
            choose: (title, message, options) => { calls.Add($"{title}|{message}|{string.Join(",", options)}"); return Task.FromResult<int?>(1); },
            confirmWarning: (title, message) => { calls.Add($"{title}|{message}"); return Task.FromResult(true); },
            orderPositionsDeleted: () => calls.Add("deleted"));

        (await interaction.AskTextAsync("T", "M", "E")).Should().Be("1001");
        (await interaction.ChooseAsync("T", "M", ["A", "B"])).Should().Be(1);
        (await interaction.ConfirmWarningAsync("T", "M")).Should().BeTrue();
        interaction.OrderPositionsDeleted();

        calls.Should().Equal("T|M|E", "T|M|A,B", "T|M", "deleted");
    }
}
