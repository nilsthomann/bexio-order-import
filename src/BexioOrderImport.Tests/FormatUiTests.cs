using System.Globalization;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Wpf.Converters;
using BexioOrderImport.Wpf.Resources;
using BexioOrderImport.Wpf.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BexioOrderImport.Tests;

public class FormatUiTests
{
    private static IFormatUiProvider Ui(string formatId)
    {
        var ui = new Mock<IFormatUiProvider>();
        ui.Setup(u => u.FormatId).Returns(formatId);
        return ui.Object;
    }

    private static readonly FormatRegistry Formats = new([new PeakmileFormatAdapter()]);

    [Test]
    public void FormatUiRegistry_Find_ShouldReturnTheProviderOfTheFormat()
    {
        var peakmileUi = Ui("peakmile");
        var registry = new FormatUiRegistry(Formats, [peakmileUi]);

        registry.Find("Peakmile").Should().BeSameAs(peakmileUi);
        registry.Find("ean-list").Should().BeNull();
        registry.Find(null).Should().BeNull();
    }

    [Test]
    public void FormatUiRegistry_WhenFormatHasNoProvider_ShouldThrow()
    {
        Action act = () => _ = new FormatUiRegistry(Formats, [Ui("other")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*peakmile*");
    }

    [Test]
    public void FormatUiRegistry_WhenFormatHasTwoProviders_ShouldThrow()
    {
        Action act = () => _ = new FormatUiRegistry(Formats, [Ui("peakmile"), Ui("PEAKMILE")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one*");
    }

    [Test]
    public void FormatUiRegistry_WhenArgumentIsNull_ShouldThrow()
    {
        Action withoutFormats = () => _ = new FormatUiRegistry(null!, []);
        Action withoutProviders = () => _ = new FormatUiRegistry(Formats, null!);

        withoutFormats.Should().Throw<ArgumentNullException>();
        withoutProviders.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void AddFormat_ShouldRegisterAdapterAndUiProvider()
    {
        var provider = new ServiceCollection()
            .AddFormat<PeakmileFormatAdapter, PeakmileUiProvider>()
            .BuildServiceProvider();

        provider.GetServices<IFormatAdapter>().Should().ContainSingle().Which.Should().BeOfType<PeakmileFormatAdapter>();
        provider.GetServices<IFormatUiProvider>().Should().ContainSingle().Which.Should().BeOfType<PeakmileUiProvider>();
    }

    [Test]
    public void FormatNameConverter_ShouldReturnTheDisplayNameOfTheFormat()
    {
        var converter = new FormatNameConverter();

        var name = converter.Convert(["peakmile", Formats.Formats], typeof(string), null!, CultureInfo.InvariantCulture);

        name.Should().Be(new PeakmileFormatAdapter().DisplayName);
    }

    [Test]
    public void FormatNameConverter_WhenFormatIsUnknown_ShouldMarkItUnavailable()
    {
        var converter = new FormatNameConverter();

        var name = converter.Convert(["future-format", Formats.Formats], typeof(string), null!, CultureInfo.InvariantCulture);
        var withoutFormats = converter.Convert(["future-format"], typeof(string), null!, CultureInfo.InvariantCulture);

        name.Should().Be(string.Format(Translations.Settings_FormatUnavailable, "future-format"));
        withoutFormats.Should().Be(name);
    }

    [Test]
    public void FormatNameConverter_ConvertBack_ShouldThrow()
    {
        Action act = () => new FormatNameConverter().ConvertBack("x", [typeof(string)], null!, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }
}
