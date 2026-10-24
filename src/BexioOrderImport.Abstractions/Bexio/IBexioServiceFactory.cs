namespace BexioOrderImport.Abstractions.Bexio;

/// <summary>
/// Factory that creates an <see cref="IBexioService"/> with per-call credentials.
/// Using a factory lets us inject the API token at call time (loaded from settings)
/// while keeping the WPF layer free from directly instantiating Infrastructure types.
/// </summary>
public interface IBexioServiceFactory
{
    /// <summary>
    /// Creates a new <see cref="IBexioService"/> authenticated with the given token.
    /// </summary>
    IBexioService Create(string apiToken, int? accountId, int? taxId, int? paymentTypeId, string language = "de");
}
