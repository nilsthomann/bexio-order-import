namespace BexioOrderImport.Application.Interfaces;

/// <summary>
/// Factory that creates an <see cref="IBexioClient"/> with per-call credentials.
/// Using a factory lets us inject the API token at call time (loaded from settings)
/// while keeping the WPF layer free from directly instantiating Infrastructure types.
/// </summary>
public interface IBexioClientFactory
{
    /// <summary>
    /// Creates a new <see cref="IBexioClient"/> authenticated with the given token.
    /// </summary>
    IBexioClient Create(string apiToken, int? accountId, int? taxId);
}
