using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Tests.Utils;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class FormatRegistryTests
{
    private static IFormatAdapter Format(string id)
    {
        var format = new Mock<IFormatAdapter>();
        format.Setup(f => f.FormatId).Returns(id);
        return format.Object;
    }

    [Test]
    public void Find_ShouldIgnoreCase()
    {
        var registry = new FormatRegistry([new FakeFormatAdapter()]);

        registry.Find("FAKE").Should().BeOfType<FakeFormatAdapter>();
    }

    [Test]
    public void Find_WhenUnknownOrNull_ShouldReturnNull()
    {
        var registry = new FormatRegistry([new FakeFormatAdapter()]);

        registry.Find("ean-list").Should().BeNull();
        registry.Find(null).Should().BeNull();
    }

    [Test]
    public void Formats_ShouldKeepRegistrationOrder()
    {
        var registry = new FormatRegistry([Format("b"), Format("a")]);

        registry.Formats.Select(f => f.FormatId).Should().Equal("b", "a");
    }

    [Test]
    public void Constructor_WhenFormatIdIsRegisteredTwice_ShouldThrow()
    {
        Action act = () => _ = new FormatRegistry([Format("peakmile"), Format("Peakmile")]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*peakmile*");
    }

    [Test]
    public void Constructor_WhenFormatsIsNull_ShouldThrow()
    {
        Action act = () => _ = new FormatRegistry(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
