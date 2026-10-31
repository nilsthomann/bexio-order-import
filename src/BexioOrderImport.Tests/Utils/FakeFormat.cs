using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Tests.Utils;

/// <summary>
/// Import format for host tests, independent of the real formats.
/// </summary>
public sealed class FakeFormatAdapter : FormatAdapter<FakeSettings>
{
    public const string Id = "fake";

    public override string FormatId => Id;

    public override string DisplayName => "Fake format";

    public override string Description => "Format of the host tests";

    /// <summary>
    /// Order returned by <see cref="Parse"/>, unless <see cref="ParseFunc"/> is set.
    /// </summary>
    public Order ParsedOrder { get; set; } = new()
    {
        Customer = new Customer { CompanyName = "Fake Customer AG" },
        Positions = [new OrderPosition { ArticleNumber = "A1", Quantity = 2, GrossUnitPrice = 10m }]
    };

    public Func<string, Order>? ParseFunc { get; set; }

    /// <summary>
    /// Thrown after the order is reported, like a failed position upload.
    /// </summary>
    public Exception? UploadError { get; set; }

    /// <summary>
    /// Reports the order as an existing one instead of a new one.
    /// </summary>
    public bool UsesExistingOrder { get; set; }

    /// <summary>
    /// Reports that the positions of the order were deleted before the upload.
    /// </summary>
    public bool DeletesPositions { get; set; }

    protected override Order Parse(string filePath, FakeSettings settings) => ParseFunc?.Invoke(filePath) ?? ParsedOrder;

    /// <summary>
    /// Asks for the upload confirmation and creates the order in Bexio.
    /// </summary>
    protected override async Task<ImportResult> ImportAsync(Order order, FakeSettings settings, IBexioService bexio, IImportUserInteractionService interaction)
    {
        if (!await interaction.ConfirmUploadAsync())
        {
            return new ImportResult(Success: false, ErrorMessage: "Cancelled by user.");
        }

        var created = await bexio.CreateOrderAsync(1, order);
        interaction.OrderResolved(created.Id, created.DocumentNr, isNew: !UsesExistingOrder);
        if (DeletesPositions)
        {
            interaction.OrderPositionsDeleted();
        }
        if (UploadError != null)
        {
            throw UploadError;
        }
        interaction.ReportProgress(1, 1);
        return new ImportResult(Success: true, OrderNumber: created.DocumentNr, UploadedPositionsCount: order.Positions.Count);
    }
}

public sealed class FakeSettings : IFormatSettings
{
    public string Value { get; set; } = "default";
}

public sealed class FakeUiProvider : IFormatUiProvider
{
    public string FormatId => FakeFormatAdapter.Id;

    public IProfileConfigControl CreateProfileConfigControl() => new FakeProfileConfigControl();

    public IImportPreviewControl CreateImportPreviewControl() => new FakePreviewControl();

    private sealed class FakeProfileConfigControl : IProfileConfigControl
    {
        private FakeSettings _settings = new();

        public FrameworkElement View { get; } = new Border();

        public void Load(IFormatSettings settings) => _settings = (FakeSettings)settings;

        public IReadOnlyList<string> Validate() => [];

        public IFormatSettings Save() => _settings;
    }

    private sealed class FakePreviewControl : IImportPreviewControl
    {
        public FrameworkElement HeaderView { get; } = new Border();

        public FrameworkElement PositionsView { get; } = new Border();

        public void Show(Order order, IFormatSettings settings)
        {
            HeaderView.DataContext = order;
            PositionsView.DataContext = order;
        }

        public void Clear()
        {
            HeaderView.DataContext = null;
            PositionsView.DataContext = null;
        }
    }
}
