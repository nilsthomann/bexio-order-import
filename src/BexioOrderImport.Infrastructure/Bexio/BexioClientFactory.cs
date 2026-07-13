using BexioOrderImport.Application.Interfaces;

namespace BexioOrderImport.Infrastructure.Bexio;

/// <summary>
/// Creates <see cref="BexioApiClient"/> instances on demand.
/// Registered as a singleton in the DI container so the underlying
/// <see cref="HttpClient"/> is reused across calls.
/// </summary>
public class BexioClientFactory : IBexioClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BexioClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IBexioClient Create(string apiToken, int? accountId, int? taxId, string language = "de")
    {
        var httpClient = _httpClientFactory.CreateClient("BexioApi");
        return new BexioApiClient(httpClient, apiToken, accountId, taxId, language);
    }
}
