using BexioOrderImport.Wpf.ViewModels;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class RelayCommandTests
{
    [Fact]
    public void RelayCommand_Constructor_NullAction_ThrowsException()
    {
        Action act = () => new RelayCommand(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RelayCommand_CanExecute_WithoutCanExecuteFunc_ReturnsTrue()
    {
        var cmd = new RelayCommand(() => { });
        cmd.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void RelayCommand_CanExecute_WithCanExecuteFunc_ReturnsCorrectValue()
    {
        bool allowed = false;
        var cmd = new RelayCommand(() => { }, () => allowed);
        cmd.CanExecute(null).Should().BeFalse();
        allowed = true;
        cmd.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void RelayCommand_Execute_RunsAction()
    {
        bool executed = false;
        var cmd = new RelayCommand(() => executed = true);
        cmd.Execute(null);
        executed.Should().BeTrue();
    }

    [Fact]
    public void RelayCommand_RaiseCanExecuteChanged_DoesNotThrow()
    {
        var cmd = new RelayCommand(() => { });
        Action act = () => cmd.RaiseCanExecuteChanged();
        act.Should().NotThrow();
    }

    [Fact]
    public void RelayCommand_CanExecuteChangedEvent_SubscribeAndUnsubscribe_DoesNotThrow()
    {
        var cmd = new RelayCommand(() => { });
        static void handler(object? s, EventArgs e) { }
        Action act = () =>
        {
            cmd.CanExecuteChanged += handler;
            cmd.CanExecuteChanged -= handler;
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void RelayCommandGeneric_Constructor_NullAction_ThrowsException()
    {
        Action act = () => new RelayCommand<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RelayCommandGeneric_CanExecute_InvalidParameterType_ReturnsFalse()
    {
        var cmd = new RelayCommand<string>(_ => { });
        cmd.CanExecute(123).Should().BeFalse(); // Int instead of string
        cmd.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void RelayCommandGeneric_CanExecute_ValidParameterType_ReturnsTrue()
    {
        var cmd = new RelayCommand<string>(_ => { });
        cmd.CanExecute("test").Should().BeTrue();
    }

    [Fact]
    public void RelayCommandGeneric_CanExecute_WithCanExecuteFunc_ReturnsCorrectValue()
    {
        var cmd = new RelayCommand<string>(_ => { }, val => val == "allow");
        cmd.CanExecute("deny").Should().BeFalse();
        cmd.CanExecute("allow").Should().BeTrue();
    }

    [Fact]
    public void RelayCommandGeneric_Execute_InvalidParameterType_DoesNotExecute()
    {
        bool executed = false;
        var cmd = new RelayCommand<string>(_ => executed = true);
        cmd.Execute(123);
        executed.Should().BeFalse();
    }

    [Fact]
    public void RelayCommandGeneric_Execute_ValidParameterType_Executes()
    {
        string? result = null;
        var cmd = new RelayCommand<string>(val => result = val);
        cmd.Execute("success");
        result.Should().Be("success");
    }

    [Fact]
    public void RelayCommandGeneric_CanExecuteChangedEvent_SubscribeAndUnsubscribe_DoesNotThrow()
    {
        var cmd = new RelayCommand<string>(_ => { });
        static void handler(object? s, EventArgs e) { }
        Action act = () =>
        {
            cmd.CanExecuteChanged += handler;
            cmd.CanExecuteChanged -= handler;
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void RelayCommandGeneric_RaiseCanExecuteChanged_DoesNotThrow()
    {
        var cmd = new RelayCommand<string>(_ => { });
        Action act = () => cmd.RaiseCanExecuteChanged();
        act.Should().NotThrow();
    }
}
