namespace BexioOrderImport.Format.Sgd.Tests.Utils;

/// <summary>
/// HTTP handler of the Bexio client in tests; no request leaves the test process.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncFunc { get; set; } = null!;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return SendAsyncFunc(request, cancellationToken);
    }
}
