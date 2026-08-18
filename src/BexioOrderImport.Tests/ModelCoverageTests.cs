using BexioOrderImport.Application.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Helpers;
using FluentAssertions;

namespace BexioOrderImport.Tests;

[NotInParallel]
public class ModelCoverageTests : StaTestBase
{
    [Test]
    public void BindingProxy_DataPropertyAndClone_ShouldWork()
    {
        RunInSta(() =>
        {
            var proxy = new BindingProxy();
            proxy.Data = "sample payload";
            proxy.Data.Should().Be("sample payload");

            var cloned = (BindingProxy)proxy.Clone();
            cloned.Should().NotBeNull();
        });
    }

    [Test]
    public void BexioOrder_Properties_ShouldSetAndGet()
    {
        var order = new BexioOrder
        {
            Id = 123,
            DocumentNr = "DOC-001",
            Title = "Test Order",
            UserId = 2,
            MwstType = MwstType.InclMwst,
            CurrencyId = 1,
            ApiReference = "API-REF",
            ContactId = 456
        };

        order.Id.Should().Be(123);
        order.DocumentNr.Should().Be("DOC-001");
        order.Title.Should().Be("Test Order");
        order.UserId.Should().Be(2);
        order.MwstType.Should().Be(MwstType.InclMwst);
        order.CurrencyId.Should().Be(1);
        order.ApiReference.Should().Be("API-REF");
        order.ContactId.Should().Be(456);
    }

    [Test]
    public void BexioArticle_Properties_ShouldSetAndGet()
    {
        var article = new BexioArticle
        {
            Id = 10,
            Code = "ART-10",
            Name = "Initial Name",
            Description = "Initial Desc",
        };

        article.Id.Should().Be(10);
        article.Code.Should().Be("ART-10");
        article.Name.Should().Be("Initial Name");
        article.Description.Should().Be("Initial Desc");
    }

    [Test]
    public void ImportResult_Properties_ShouldWork()
    {
        var successResult = new ImportResult(Success: true, OrderId: 99, UploadedPositionsCount: 15);
        successResult.Success.Should().BeTrue();
        successResult.OrderId.Should().Be(99);
        successResult.UploadedPositionsCount.Should().Be(15);
        successResult.ErrorMessage.Should().BeNull();

        var failureResult = new ImportResult(Success: false, ErrorMessage: "Error occurred");
        failureResult.Success.Should().BeFalse();
        failureResult.OrderId.Should().BeNull();
        failureResult.UploadedPositionsCount.Should().Be(0);
        failureResult.ErrorMessage.Should().Be("Error occurred");
    }
}
