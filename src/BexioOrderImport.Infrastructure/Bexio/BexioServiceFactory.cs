using BexioOrderImport.Abstractions.Bexio;

namespace BexioOrderImport.Infrastructure.Bexio;

/// <summary>
/// Creates <see cref="BexioApiClient"/> instances on demand.
/// Registered as a singleton in the DI container so the underlying
/// <see cref="HttpClient"/> is reused across calls.
/// </summary>
public class BexioServiceFactory : IBexioServiceFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BexioServiceFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IBexioService Create(string apiToken, int? accountId, int? taxId, int? paymentTypeId, string language = "de")
    {
        var httpClient = _httpClientFactory.CreateClient("BexioApi");
        return new BexioApiClient(httpClient, apiToken, accountId, taxId, language, paymentTypeId);
    }
}
